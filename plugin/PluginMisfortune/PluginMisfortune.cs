using System;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Rainmeter;

/// <summary>
/// A plugin that reads "fortune" database files and returns a random fortune.
///
/// It's necessary to do this in C# rather than Lua / a .ini file because they
/// have no way to list files in a directory. No, seriously. Lua doesn't come
/// with anything and os.popen is disabled.
///
/// This isn't in C++ because I'm not hardcore enough.
/// </summary>
namespace PluginMisfortune
{
    internal class Measure
    {
        internal string dirpath;
        internal string currentFortune;
        internal FortunesMetadata metadata;
        internal FortunesMetadata.FileMatcher matcher;

        internal Measure()
        {
            this.currentFortune = "You are destined to not have loaded any fortunes yet.";
        }

        internal void Reload(Rainmeter.API api, ref double maxValue)
        {
            this.dirpath = api.ReadPath("FortunesDir", api.ReplaceVariables("#@#fortunes"));

            string prefixes = api.ReadString("Prefixes", null);
            string regex = api.ReadString("Regex", null);

            if (prefixes != null && regex != null)
            {
                Rainmeter.API.Log(Rainmeter.API.LogType.Warning,
                    "Both 'Prefixes' and 'Regexes' are set. Arbitrarily selecting Prefixes.");
            }

            if (prefixes != null)
            {
                string[] prefixesSeparate = prefixes.Split(';');
                this.matcher = (fileName) =>
                {
                    foreach (string prefix in prefixesSeparate)
                    {
                        if (fileName.StartsWith(prefix))
                        {
                            return true;
                        }
                    }
                    return false;
                };
            }
            else if (regex != null)
            {
                Regex re = new Regex(regex);
                this.matcher = re.IsMatch;
            }
            else
            {
                this.matcher = (fileName) => true;
            }

            try
            {
                this.metadata = FortunesMetadata.LoadFrom(this.dirpath);
                this.metadata.Refresh();
            }
            catch (Exception e)
            {
                this.currentFortune =
                    "You are destined to encounter errors with the Misfortune rainmeter plugin.\nSee log for details.";
                Rainmeter.API.Log(Rainmeter.API.LogType.Error, e.ToString());
            }
        }

        // Return a new fortune every time we reload.
        internal double Update()
        {
            this.currentFortune = this.metadata.GetRandomMatching(this.matcher);

            // Always return 0, what do you want?
            return 0.0;
        }
        
        internal string GetString()
        {
            return this.currentFortune;
        }
    }

    public static class Plugin
    {
        static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            GCHandle.FromIntPtr(data).Free();
            
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Reload(new Rainmeter.API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }
        
        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = measure.GetString();
            if (stringValue != null)
            {
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);
            }

            return StringBuffer;
        }
    }
}
