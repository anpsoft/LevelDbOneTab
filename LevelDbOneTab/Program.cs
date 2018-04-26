using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

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

                var keys = new string[] {
                    "state", "topSites", "settings", "lastSeenVersion",
                    "installDate", "idCounter", "extensionKey"
                };

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
                        byte[] data = new byte[bufferLen];

                        // Read from unmanaged memory to the byte array.
                        ums.Read(data, 0, (int)bufferLen);

                        // Save data as is 
                        DbSaveBinary(add + ".bin");

                        // Save data as formated json 
                        if (mode) {
                            string s = Encoding.Unicode.GetString(data);
                            File.WriteAllText(add + ".json", FormatJson2(s));
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



        private const string INDENT_STRING = "    ";

        static string FormatJson(string json) {
            int indentation = 0;
            int quoteCount = 0;
            var result =
                from ch in json
                let quotes = ch == '"' ? quoteCount++ : quoteCount
                let lineBreak = ch == ',' && quotes % 2 == 0 ? ch + Environment.NewLine + String.Concat(Enumerable.Repeat(INDENT_STRING, indentation)) : null
                let openChar = ch == '{' || ch == '[' ? ch + Environment.NewLine + String.Concat(Enumerable.Repeat(INDENT_STRING, ++indentation)) : ch.ToString()
                let closeChar = ch == '}' || ch == ']' ? Environment.NewLine + String.Concat(Enumerable.Repeat(INDENT_STRING, --indentation)) + ch : ch.ToString()
                select lineBreak == null
                            ? openChar.Length > 1
                                ? openChar
                                : closeChar
                            : lineBreak;

            return String.Concat(result);
        }


        private const int INDENT_SIZE = 2;

        public static string FormatJson2(string str) {
            str = (str ?? "").Replace("{}", @"\{\}").Replace("[]", @"\[\]");

            var inserts = new List<int[]>();
            bool quoted = false, escape = false;
            int depth = 0/*-1*/;

            for (int i = 0, N = str.Length; i < N; i++) {
                var chr = str[i];

                if (!escape && !quoted)
                    switch (chr) {
                        case '{':
                        case '[':
                            inserts.Add(new[] { i, +1, 0, INDENT_SIZE * ++depth });
                            //int n = (i == 0 || "{[,".Contains(str[i - 1])) ? 0 : -1;
                            //inserts.Add(new[] { i, n, INDENT_SIZE * ++depth * -n, INDENT_SIZE - 1 });
                            break;
                        case ',':
                            inserts.Add(new[] { i, +1, 0, INDENT_SIZE * depth });
                            //inserts.Add(new[] { i, -1, INDENT_SIZE * depth, INDENT_SIZE - 1 });
                            break;
                        case '}':
                        case ']':
                            inserts.Add(new[] { i, -1, INDENT_SIZE * --depth, 0 });
                            //inserts.Add(new[] { i, -1, INDENT_SIZE * depth--, 0 });
                            break;
                        case ':':
                            inserts.Add(new[] { i, 0, 1, 1 });
                            break;
                    }

                quoted = (chr == '"') ? !quoted : quoted;
                escape = (chr == '\\') ? !escape : false;
            }

            if (inserts.Count > 0) {
                var sb = new System.Text.StringBuilder(str.Length * 2);

                int lastIndex = 0;
                foreach (var insert in inserts) {
                    int index = insert[0], before = insert[2], after = insert[3];
                    bool nlBefore = (insert[1] == -1), nlAfter = (insert[1] == +1);

                    sb.Append(str.Substring(lastIndex, index - lastIndex));

                    if (nlBefore) sb.AppendLine();
                    if (before > 0) sb.Append(new String(' ', before));

                    sb.Append(str[index]);

                    if (nlAfter) sb.AppendLine();
                    if (after > 0) sb.Append(new String(' ', after));

                    lastIndex = index + 1;
                }

                str = sb.ToString();
            }

            return str.Replace(@"\{\}", "{}").Replace(@"\[\]", "[]");
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