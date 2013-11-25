﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jil.Common;
using Sigil.NonGeneric;
using System.Reflection;

namespace Jil.Deserialize
{
    // Note that WhiteSpace is always implicit allowed to "end" in place of a token
    [Flags]
    enum ExpectedEndMarker : byte
    {
        Undefined = 0,

        EndOfStream = 1,
        Comma = 2,
        CurlyBrace = 4,
        SquareBrace = 8
    }

    class InlineDeserializer<ForType>
    {
        const string CharBufferName = "char_buffer";
        const string StringBuilderName = "string_builder";
        
        readonly Type RecursionLookupType;

        Emit Emit;

        public InlineDeserializer(Type recursionLookupType)
        {
            RecursionLookupType = recursionLookupType;
        }

        void AddGlobalVariables()
        {
            Emit.DeclareLocal<char[]>(CharBufferName);
            Emit.LoadConstant(Methods.CharBufferSize);
            Emit.NewArray<char>();
            Emit.StoreLocal(CharBufferName);

            Emit.DeclareLocal<StringBuilder>(StringBuilderName);
            Emit.NewObject<StringBuilder>();
            Emit.StoreLocal(StringBuilderName);
        }

        void LoadCharBuffer()
        {
            Emit.LoadLocal(CharBufferName);
        }

        void LoadStringBuilder()
        {
            Emit.LoadLocal(StringBuilderName);
        }

        static MethodInfo TextReader_Read = typeof(TextReader).GetMethod("Read", Type.EmptyTypes);
        void RawReadChar(Action endOfStream)
        {
            var haveChar = Emit.DefineLabel();

            Emit.LoadArgument(0);                       // TextReader
            Emit.CallVirtual(TextReader_Read);          // int
            Emit.Duplicate();                           // int int
            Emit.LoadConstant(-1);                      // int int -1
            Emit.UnsignedBranchIfNotEqual(haveChar);    // int
            Emit.Pop();                                 // --empty--
            endOfStream();                              // --empty--

            Emit.MarkLabel(haveChar);                   // int
        }

        void ThrowExpected(char c)
        {
            Emit.LoadConstant("Expected character: '" + c + "'");   // string
            Emit.NewObject<DeserializationException, string>();     // DeserializationException
            Emit.Throw();
        }

        void ThrowExpected(params object[] ps)
        {
            Emit.LoadConstant("Expected: " + string.Join(", ", ps));    // string
            Emit.NewObject<DeserializationException, string>();         // DeserializationException
            Emit.Throw();
        }

        void ExpectChar(char c)
        {
            var gotChar = Emit.DefineLabel();

            RawReadChar(() => ThrowExpected(c));  // int
            Emit.LoadConstant((int)c);            // int "
            Emit.BranchIfEqual(gotChar);           // --empty--
            ThrowExpected(c);                     // --empty--

            Emit.MarkLabel(gotChar);               // --empty--
        }

        void ExpectQuote()
        {
            ExpectChar('"');
        }

        void ExpectQuoteOrNull(Action ifQuote, Action ifNull)
        {
            var gotQuote = Emit.DefineLabel();
            var gotN = Emit.DefineLabel();
            var done = Emit.DefineLabel();

            RawReadChar(() => ThrowExpected("\"", "null")); // int
            Emit.Duplicate();                               // int int
            Emit.LoadConstant((int)'"');                    // int int "
            Emit.BranchIfEqual(gotQuote);                   // int 
            Emit.LoadConstant((int)'n');                    // int n
            Emit.BranchIfEqual(gotN);                       // --empty--
            ThrowExpected("\"", "null");                    // --empty--

            Emit.MarkLabel(gotQuote);                       // int
            Emit.Pop();                                     // --empty--
            ifQuote();                                      // ???
            Emit.Branch(done);                              // ???

            Emit.MarkLabel(gotN);                           // --empty--
            ExpectChar('u');                                // --empty--
            ExpectChar('l');                                // --empty--
            ExpectChar('l');                                // --empty--
            ifNull();                                       // ???

            Emit.MarkLabel(done);                           // --empty--
        }

        void ReadChar()
        {
            ExpectQuote();          // --empty--
            ReadEncodedChar();      // char
            ExpectQuote();          // char
        }

        void ReadEncodedChar()
        {
            Emit.LoadArgument(0);               // TextReader
            LoadCharBuffer();
            Emit.Call(Methods.ReadEncodedChar); // char
        }

        void ReadString()
        {
            ExpectQuoteOrNull(
                delegate
                {
                    // --empty--
                    Emit.LoadArgument(0);                   // TextReader
                    LoadCharBuffer();                       // TextReader char[]
                    LoadStringBuilder();                    // TextReader char[] StringBuilder
                    Emit.Call(Methods.ReadEncodedString);   // string
                },
                delegate
                {
                    Emit.LoadNull();                        // null
                }
            );
        }

        static MethodInfo GetReadUInt8(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadUInt8TillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadInt8(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadInt8TillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadInt16(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadInt16TillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadUInt16(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadUInt16TillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadInt32(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadInt32TillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadUInt32(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadUInt32TillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadInt64(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadInt64TillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadUInt64(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadUInt64TillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadDouble(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadDoubleTillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadSingle(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadSingleTillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        static MethodInfo GetReadDecimal(ExpectedEndMarker marker)
        {
            switch (marker)
            {
                case ExpectedEndMarker.EndOfStream: return Methods.ReadDecimalTillEnd;
                default: throw new Exception("Unexpected ExpectedEndMarker: " + marker);
            }
        }

        void ReadNumber(Type numberType, ExpectedEndMarker end)
        {
            Emit.LoadArgument(0);               // TextReader

            if (numberType == typeof(byte))
            {
                Emit.Call(GetReadUInt8(end));   // byte
                return;
            }

            if (numberType == typeof(sbyte))
            {
                Emit.Call(GetReadInt8(end));    // sbyte
                return;
            }

            if (numberType == typeof(short))
            {
                Emit.Call(GetReadInt16(end));   // short
                return;
            }

            if (numberType == typeof(ushort))
            {
                Emit.Call(GetReadUInt16(end));  // ushort
                return;
            }

            if (numberType == typeof(int))
            {
                Emit.Call(GetReadInt32(end));   // int
                return;
            }

            if (numberType == typeof(uint))
            {
                Emit.Call(GetReadUInt32(end));  // uint
                return;
            }

            if (numberType == typeof(long))
            {
                Emit.Call(GetReadInt64(end));   // long
                return;
            }

            if (numberType == typeof(ulong))
            {
                Emit.Call(GetReadUInt64(end));  // ulong
                return;
            }

            LoadStringBuilder();                    // TextReader StringBuilder

            if (numberType == typeof(double))
            {
                Emit.Call(GetReadDouble(end));   // double
                return;
            }

            if (numberType == typeof(float))
            {
                Emit.Call(GetReadSingle(end));  // float
                return;
            }

            if (numberType == typeof(decimal))
            {
                Emit.Call(GetReadDecimal(end)); // decimal
                return;
            }

            throw new Exception("Unexpected number type: " + numberType);
        }

        void ReadPrimitive(Type primitiveType, ExpectedEndMarker end)
        {
            if (primitiveType == typeof(char))
            {
                ReadChar();
                return;
            }

            if (primitiveType == typeof(string))
            {
                ReadString();
                return;
            }

            ReadNumber(primitiveType, end);
        }

        void ConsumeWhiteSpace()
        {
            Emit.LoadArgument(0);                   // TextReader
            Emit.Call(Methods.ConsumeWhiteSpace);   // --empty--
        }

        void ExpectEndOfStream()
        {
            var success = Emit.DefineLabel();

            Emit.LoadArgument(0);           // TextReader
            Emit.Call(TextReader_Read);     // int
            Emit.LoadConstant(-1);          // int -1
            Emit.BranchIfEqual(success);    // --empty--

            Emit.LoadConstant("Expected end of stream");        // string
            Emit.NewObject<DeserializationException, string>(); // DeserializationException
            Emit.Throw();                                       // --empty--

            Emit.MarkLabel(success);        // --empty--
        }

        void LoadEnumValue(Type enumType, object val)
        {
            // TODO: ... all of it
            Emit.LoadConstant(0);
        }

        void ReadEnum(Type enumType, Dictionary<char, List<Tuple<string, object>>> byChar, Sigil.Label finished)
        {
            var labels = byChar.ToDictionary(d => d.Key, d => Emit.DefineLabel());

            ReadEncodedChar();  // char

            foreach (var kv in byChar)
            {
                var ifMatchesChar = kv.Key;
                var gotoLabel = labels[ifMatchesChar];

                Emit.Duplicate();                   // char char
                Emit.LoadConstant(ifMatchesChar);   // char char char
                Emit.BranchIfEqual(gotoLabel);      // char
            }

            Emit.Pop();
            Emit.LoadConstant("Expected an instance of the " + enumType.FullName + " enum");
            Emit.NewObject<DeserializationException, string>();
            Emit.Throw();

            foreach (var kv in byChar)
            {
                var label = labels[kv.Key];
                var remainingOptions = byChar[kv.Key];

                if (remainingOptions.Count == 1)
                {
                    ExpectQuote();

                    LoadEnumValue(enumType, remainingOptions.Single().Item2);
                    Emit.Branch(finished);
                    continue;
                }

                var exactMatch = remainingOptions.Where(t => t.Item1 == "").SingleOrDefault();
                if (exactMatch != null)
                {
                    var notMatch = Emit.DefineLabel();

                    RawReadChar(() => ThrowExpected("any character"));  // char
                    Emit.LoadConstant('"');                             // char "
                    Emit.UnsignedBranchIfNotEqual(notMatch);            // --empty--

                    LoadEnumValue(enumType, exactMatch.Item2);
                    Emit.Branch(finished);

                    Emit.MarkLabel(notMatch);
                }

                var withoutNextChar =
                    remainingOptions.Select(t => Tuple.Create(new string(t.Item1.Skip(1).ToArray()), t.Item2));

                var asDict =
                    withoutNextChar.GroupBy(g => g.Item1[0]).ToDictionary(g => g.Key, g => g.ToList());

                ReadEnum(enumType, asDict, finished);
            }
        }

        void ReadEnum(Type enumType, ExpectedEndMarker end)
        {
            var names = Enum.GetNames(enumType);
            var byChar = names.GroupBy(g => g[0]).ToDictionary(g => g.Key, g => g.Select(t => Tuple.Create(t, Enum.Parse(enumType, t))).ToList());
            var finished = Emit.DefineLabel();

            ExpectQuote();
            ReadEnum(enumType, byChar, finished);

            Emit.MarkLabel(finished);
        }

        Func<TextReader, int, ForType> BuildPrimitiveWithNewDelegate()
        {
            Emit = Emit.NewDynamicMethod(typeof(ForType), new[] { typeof(TextReader), typeof(int) });

            AddGlobalVariables();

            ConsumeWhiteSpace();

            ReadPrimitive(typeof(ForType), ExpectedEndMarker.EndOfStream);

            // we have to consume this, otherwise we might succeed with invalid JSON
            ConsumeWhiteSpace();

            // We also must confirm that we read everything, again otherwise we might accept garbage as valid
            ExpectEndOfStream();

            Emit.Return();

            return Emit.CreateDelegate<Func<TextReader, int, ForType>>();
        }

        Func<TextReader, int, ForType> BuildDictionaryWithNewDelegate()
        {
            throw new NotImplementedException();
        }

        Func<TextReader, int, ForType> BuildObjectWithNewDelegate()
        {
            throw new NotImplementedException();
        }

        Func<TextReader, int, ForType> BuildListWithNewDelegate()
        {
            throw new NotImplementedException();
        }

        Func<TextReader, int, ForType> BuildEnumWithNewDelegate()
        {
            Emit = Emit.NewDynamicMethod(typeof(ForType), new[] { typeof(TextReader), typeof(int) });

            AddGlobalVariables();

            ConsumeWhiteSpace();

            ReadEnum(typeof(ForType), ExpectedEndMarker.EndOfStream);

            // we have to consume this, otherwise we might succeed with invalid JSON
            ConsumeWhiteSpace();

            // We also must confirm that we read everything, again otherwise we might accept garbage as valid
            ExpectEndOfStream();

            Emit.Return();

            return Emit.CreateDelegate<Func<TextReader, int, ForType>>();
        }

        Func<TextReader, int, ForType> BuildNullableWithNewDelegate()
        {
            throw new NotImplementedException();
        }

        public Func<TextReader, int, ForType> Build()
        {
            var forType = typeof(ForType);

            if (forType.IsNullableType())
            {
                return BuildNullableWithNewDelegate();
            }

            if (forType.IsPrimitiveType())
            {
                return BuildPrimitiveWithNewDelegate();
            }

            if (forType.IsDictionaryType())
            {
                return BuildDictionaryWithNewDelegate();
            }

            if (forType.IsListType())
            {
                return BuildListWithNewDelegate();
            }

            if (forType.IsEnum)
            {
                return BuildEnumWithNewDelegate();
            }

            return BuildObjectWithNewDelegate();
        }
    }

    static class InlineDeserializerHelper
    {
        public static Func<TextReader, int, ReturnType> Build<ReturnType>(Type typeCacheType)
        {
            var obj = new InlineDeserializer<ReturnType>(typeCacheType);

            var ret = obj.Build();

            return ret;
        }
    }
}