﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TUP.AsmResolver
{
    /// <summary>
    /// Represents a procedure or method that is inside a library or other portable executable file and is used by the PE file who imported this method.
    /// </summary>
    public class ImportMethod : IMethod
    {

        public ImportMethod( uint ofunction, uint rva, ushort hint, string name)
        {
            this.Value = ofunction;
            this.RVA = rva;
            this.Ordinal = hint;
            this.Name = name;
        }

        public LibraryReference ParentLibrary
        {
            get;
            internal set;
        }

        public string Name
        {
            get;
            private set;
        }

        public string FullName
        {
            get { return (LibraryName != string.Empty ? LibraryName + "." + Name : Name); }
        }

        public string LibraryName
        {
            get { return (ParentLibrary != null ? ParentLibrary.LibraryName : string.Empty); }
        }

        public uint Ordinal
        {
            get;
            private set;
        }

        public uint RVA
        {
            get;
            private set;
        }
        public uint Value
        {
            get;
            private set;
        }
    }
}
