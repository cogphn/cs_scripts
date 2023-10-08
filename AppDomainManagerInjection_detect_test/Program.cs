using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace adm_test {
    internal class Program {


        //public static int lookback = 3600000; // 1hr
        public static int lookback = 1800000; // 1/2 hr

        static int GetEvents() {
            string logname = "Microsoft-Windows-Sysmon/Operational";
            string filter = String.Format("*[System/EventID=7][System[TimeCreated[timediff(@SystemTime)<{0}]]]", lookback);

            EventLogQuery query = new EventLogQuery(logname, PathType.LogName, filter);
            EventLogReader reader = new EventLogReader(query);

            for(EventRecord e = reader.ReadEvent(); e != null; e = reader.ReadEvent()) {


                string signed = e.Properties[12].Value.ToString();
                if (signed.ToUpper().Equals("TRUE")) {
                    // signed
                    continue;
                }
                string image = e.Properties[4].Value.ToString();
                string imageloaded = e.Properties[5].Value.ToString();

                var image_path = Path.GetDirectoryName(image);
                var image_base_name = Path.GetFileName(image);

                var imageloaded_path = Path.GetDirectoryName(imageloaded);
                var imageloaded_base_name = Path.GetFileName(imageloaded);
                if(image.ToString().ToUpper().Equals(imageloaded.ToString().ToUpper())) {
                    // unsigned binary loading itself
                    continue;
                }
                string[] files_in_path = Directory.GetFiles(image_path.ToString(), "*.*");

                foreach (string f in files_in_path) {
                    string filename = Path.GetFileName(f);

                    if (filename.Equals(String.Format("{0}.config",image_base_name), StringComparison.OrdinalIgnoreCase)) {
                        // Configuration file exists
                        string configfile_text = File.ReadAllText(f);
                        string assembly_name = Path.GetFileNameWithoutExtension(imageloaded);
                        
                        if (configfile_text.IndexOf(assembly_name, StringComparison.OrdinalIgnoreCase) >=0) {
                            Console.WriteLine("    [!] Suspicious Unsigned image load: ");
                            Console.WriteLine(String.Format("    {0}", image));
                            Console.WriteLine(String.Format("       ->{0}", imageloaded));
                            using(EventLog app_event_log = new EventLog("Application")) {
                                app_event_log.Source = "jankbot-9000";
                                app_event_log.WriteEntry(String.Format("Possible AppDomain manager injection: {0} -> {1}", image, imageloaded), EventLogEntryType.Warning, 251);
                            }
                        }
                    } 

                    
                }

                /* 
                 * Additional checks to run:
                 * 1.Check for env variables  
                 * 2. executable file or unsigned bin no longer exist -> suspicious 
                */
            }

            return -1;
        }
        static void Main(string[] args) {
            Console.WriteLine("[*] Running...");
            var test = GetEvents();
            Console.WriteLine("[.] Done!");
        }
    }
}
