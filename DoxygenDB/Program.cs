using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoxygenDB
{
    class Program
    {
        static void Main(string[] args)
        {
            DoxygenDB db = new DoxygenDB();
            //var filePath = "I:/Programs/vsAddin/CodeAtlasVSIX/DoxygenDB/doxyFile";
            ////db._GenerateConfigureFile(filePath);
            //var metaDict = new Dictionary<string, List<string>>();
            //db._ReadDoxyfile("I:/Programs/mitsuba/doxyfile", metaDict);
            //db._WriteDoxyfile("I:/Programs/vsAddin/CodeAtlasVSIX/DoxygenDB/doxyFile", metaDict);
            //db.Open("D:/Code/NewRapidRT/rapidrt/Doxyfile");

            var config = new DoxygenDBConfig();
            config.m_configPath = "I:/Programs/masteringOpenCV/Chapter3_MarkerlessAR/doxyfile";
            config.m_inputFolders = new List<string> { "I:/Programs/masteringOpenCV/Chapter3_MarkerlessAR/src" };
            config.m_projectName = "AR";
            config.m_outputDirectory = "I:/Programs/masteringOpenCV/Chapter3_MarkerlessAR/doc";

            DoxygenDB.GenerateDB(config);
        }
    }
}
