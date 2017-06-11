using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAtlasVSIX
{
    class Logger
    {
        static bool s_initialized = false;
        static IVsOutputWindow s_outWindow;
        static IVsOutputWindowPane s_outputPane;
        static Guid s_customGuid = new Guid("FD6C44F4-521F-47B3-98BF-78C4B4B4E5A8");

        static void Initialize()
        {
            s_outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (s_outWindow != null)
            {
                //Guid generalPaneGuid = VSConstants.GUID_OutWindowGeneralPane; // P.S. There's also the GUID_OutWindowDebugPane available.
                s_outWindow.CreatePane(s_customGuid, "Code Atlas", 1, 1);
                s_outWindow.GetPane(ref s_customGuid, out s_outputPane);
            }
        }

        public static void WriteLine(string content)
        {
            if (s_initialized == false)
            {
                Initialize();
                s_initialized = true;
            }

            if (s_outputPane != null)
            {
                s_outputPane.OutputString(content + "\n");
                s_outputPane.Activate(); // Brings this pane into view
            }
            else
            {
                System.Console.WriteLine(content);
            }
        }
    }
}
