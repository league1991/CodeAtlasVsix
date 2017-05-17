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
            db.Open("D:/Code/NewRapidRT/rapidrt/Doxyfile");
        }
    }
}
