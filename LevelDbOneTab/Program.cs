using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace ConsoleApp1 {

    class Program {

        [DllImport("MyLevelDb.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool DbOpen(string path);

        [DllImport("MyLevelDb.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool DbKeyOpen(string key);

        [DllImport("MyLevelDb.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int DbKeyClose();

        [DllImport("MyLevelDb.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int DbClose();

        [DllImport("MyLevelDb.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern byte* DbGet(out UInt32 bufferLen);

        [DllImport("MyLevelDb.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool DbSaveBinary(string filename);


        //string path = "d:/_MyProjects/OneTabSorter/ExportOneTab/leveldb";
        //string path = @"c:\Users\Andrey\AppData\Local\Vivaldi\User Data\Default\Local Storage\leveldb\";

        static void Main(string[] args) {

            string path = args[0];

            if (path == "")
                Console.WriteLine("Please set path to chrome or vivaldi leveldb folder");


            // "--" is replaced by zero bytes in c++ dll
            string key = "_chrome-extension://chphlpgkkbolifaimnlloiipkdnihall--";


            if (DbOpen(path)) {

                //var keys = new string[] {
                //    "state", "topSites", "settings", "lastSeenVersion",
                //    "installDate", "idCounter", "extensionKey"
                //};
                var keys = new string[] { "oldState" };

                string[] files = (string[])keys.Clone();

                bool first = true;
                for (int i = 0; i < keys.Length; i++) {
                    DoIt(key, keys[i], first);
                    files[i] = keys[i] + ".bin";
                    first = false;
                }

                Array.Resize(ref files, files.Length + 1);
                files[files.Length - 1] = keys[0] + ".json";

                DbClose();

                string fn = string.Format("OneTab-{0:yyyy-MM-dd_hh-mm}.zip", DateTime.Now);

                ZipFileCreator.CreateZipFile(fn, files);

                foreach (var item in files)
                    if (File.Exists(item))
                        File.Delete(item);

            }
            //Console.ReadKey();
        }


        static void DoIt(string k, string add, bool mode = false) {
            if (DbKeyOpen(k + add)) {
                unsafe {
                    UInt32 bufferLen = 0;
                    byte* buffer = DbGet(out bufferLen);

                    // ++buffer - this is correction for zero byte artefact 
                    var ums = new UnmanagedMemoryStream(++buffer, (Int32)bufferLen, (Int32)bufferLen, FileAccess.Read);
                    try {

                        // Create a byte array to hold data from unmanaged memory.
                        byte[] data = new byte[bufferLen-1];

                        // Read from unmanaged memory to the byte array.
                        ums.Read(data, 0, (int)bufferLen-1);

                        // Save data as is 
                        DbSaveBinary(add + ".bin");

                        // Save data as formated json 
                        if (mode) {
                            string s = Encoding.Unicode.GetString(data);
                            File.WriteAllText(add + ".json", JToken.Parse(s).ToString(Formatting.Indented));
                        }

                    }
                    finally {
                        if (ums != null) ums.Dispose();
                        ums = null;
                    }

                    DbKeyClose();
                }
            }

        }





        // Create a ZIP file of the files provided.
        public static class ZipFileCreator {
            public static void CreateZipFile(string fileName, IEnumerable<string> files) {

                // Create and open a new ZIP file
                using (var zip = ZipFile.Open(fileName, ZipArchiveMode.Create)) {

                    // Add the entry for each file
                    foreach (var file in files) {
                        zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
                    }
                }
            }

        }




    }
}