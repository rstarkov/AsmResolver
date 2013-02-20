﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TUP.AsmResolver.PE;
using TUP.AsmResolver.NET.Specialized;
namespace TUP.AsmResolver.NET
{
    /// <summary>
    /// Represents a .NET header from an application. This header is only available if the assembly is written in a .NET language.
    /// </summary>
    public class NETHeader : IHeader , IDisposable 
    {
        
        internal Win32Assembly assembly;
        internal NETHeaderReader reader;
        internal uint flags;
        internal uint entryPointToken;
        internal StringsHeap stringsheap;
        internal UserStringsHeap usheap;
        internal TablesHeap tableheap;
        internal BlobHeap blobheap;
        internal GuidHeap guidheap;
        internal TypeSystem typeSystem;
        MetaDataHeader metadata;

        /// <summary>
        /// Gets a metadata token resolver to lookup members by its metadata token.
        /// </summary>
        public MetaDataTokenResolver TokenResolver { get; internal set; }
        /// <summary>
        /// Gets the type system class that holds all element types.
        /// </summary>
        public TypeSystem TypeSystem
        {
            get
            {
                if (typeSystem == null)
                    typeSystem = new TypeSystem(this);
                return typeSystem;
            }
        }



        public DataDirectory MetaDataDirectory { get; internal set; }
        public DataDirectory ResourcesDirectory { get; internal set; }
        public DataDirectory StrongNameDirectory { get; internal set; }
        public DataDirectory CodeManagerDirectory { get; internal set; }
        public DataDirectory VTableFixupsDirectory { get; internal set; }
        public DataDirectory ExportAddressesDirectory { get; internal set; }
        public DataDirectory ManagedNativeHeaderDirectory { get; internal set; }



        internal NETHeader()
        {
        }

        /// <summary>
        /// Gets the Portable Executeable's NT header by specifing the assembly.
        /// </summary>
        /// <param name="assembly">The assembly to read the nt header</param>
        /// <returns></returns>
        public static NETHeader FromAssembly(Win32Assembly assembly)
        {
            NETHeader header = new NETHeader();
            
            header.assembly = assembly;
            header.reader = new NETHeaderReader(assembly.ntheader, header);
            header.metadata = new MetaDataHeader(header.reader);
            header.reader.LoadData();
            header.flags = header.reader.netheader.Flags;
            header.entryPointToken = header.reader.netheader.EntryPointToken;
            header.TokenResolver = new MetaDataTokenResolver(header);
            return header;
            

        }

        /// <summary>
        /// Gets the EntryPoint Token of the loaded .NET application.
        /// </summary>
        public uint EntryPointToken
        {
            get { return entryPointToken; }
        }
        /// <summary>
        /// Gets or sets the Flags of this .NET header.
        /// </summary>
        public NETHeaderFlags Flags
        {
            get { return (NETHeaderFlags)flags; }
            set
            {
                int targetoffset = (int)RawOffset + Structures.DataOffsets[typeof(Structures.IMAGE_COR20_HEADER)][4];
                assembly.peImage.Write(targetoffset, (uint)value);
                flags = (uint)value;
            }
        }
        /// <summary>
        /// Gets the header of the MetaData.
        /// </summary>
        public MetaDataHeader MetaDataHeader
        {
            get { return metadata; }
        }
        /// <summary>
        /// Gets the metadata streams in an array.
        /// </summary>
        public MetaDataStream[] MetaDataStreams
        {
            get { return reader.metadatastreams.ToArray(); }
        }


        /// <summary>
        /// Gets the tables heap of the .net application.
        /// </summary>
        public TablesHeap TablesHeap
        {
            get 
            {
                if (tableheap == null)
                {
                    tableheap = TablesHeap.FromStream(MetaDataStreams.First(t => t.name == "#~" || t.name == "#-"));
                    tableheap.tablereader.ReadTables();
                }

                return tableheap;
            }
        }
        /// <summary>
        /// Gets the strings heap of the .net application.
        /// </summary>
        public StringsHeap StringsHeap
        {
            get
            {
                if (stringsheap == null)
                    stringsheap = StringsHeap.FromStream(MetaDataStreams.First(t => t.name == "#Strings"));
                return stringsheap;
            }
        }
        /// <summary>
        /// Gets the user specified strings heap of the .net application.
        /// </summary>
        public UserStringsHeap UserStringsHeap
        {
            get
            {
                if (usheap == null)
                    usheap = UserStringsHeap.FromStream(MetaDataStreams.First(t => t.name == "#US"));
                return usheap;
            }
        }
        /// <summary>
        /// Gets the blob heap of the .net application.
        /// </summary>
        public BlobHeap BlobHeap
        {
            get
            { 
                if (blobheap == null)
                    blobheap = BlobHeap.FromStream(MetaDataStreams.First(t => t.name == "#Blob")); 
                return blobheap;
            }
        }
        /// <summary>
        /// Gets the GUID heap of the .net application.
        /// </summary>
        public GuidHeap GuidHeap
        {
            get
            {
                if (guidheap == null)
                    guidheap = GuidHeap.FromStream(MetaDataStreams.First(t => t.name == "#GUID"));
                return guidheap;
            }
        }

        /// <summary>
        /// Gets the parent assembly container of the header.
        /// </summary>
        public Win32Assembly ParentAssembly 
        {
            get
            {
                return assembly;
            }
        }
        /// <summary>
        /// Gets the raw file offset of the header.
        /// </summary>
        public long RawOffset
        {
            get
            {
                return reader.netheaderoffset;
            }
        }

        /// <summary>
        /// Gets a value indicating the .NET header is valid.
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (MetaDataStreams == null || MetaDataStreams.Length == 0)
                    return false;
                if (!HasStream("#~") && !HasStream("#-"))
                    return false;
                if (!HasStream("#Strings"))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Returns true when a stream specified by its name is present in the assembly.
        /// </summary>
        /// <param name="name">The name of the stream.</param>
        /// <returns></returns>
        public bool HasStream(string name)
        {
            if (MetaDataStreams == null || MetaDataStreams.Length == 0)
                return false;
            return (MetaDataStreams.FirstOrDefault(s => s.name == name) != null);
        }

        /// <summary>
        /// Frees all heaps and streams that are being used.
        /// </summary>
        public void Dispose()
        {
            if (blobheap != null)
                blobheap.Dispose();
            if (guidheap != null)
                guidheap.Dispose();
            if (stringsheap != null)
                stringsheap.Dispose();
            if (usheap != null)
                usheap.Dispose();
            if (tableheap != null)
                tableheap.Dispose();

        }
    }
}
