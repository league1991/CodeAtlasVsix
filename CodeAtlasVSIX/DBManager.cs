using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAtlasVSIX
{
    class DBManager
    {
        static DBManager s_dbMgr = null;
        static int s_atlasPort = 12346;
        static int s_sublimePort = 12345;

        DoxygenDB.DoxygenDB m_db;
        
        DBManager()
        {
            m_db = new DoxygenDB.DoxygenDB();
        }

        public static DBManager Instance()
        {
            if (s_dbMgr == null)
            {
                s_dbMgr = new DBManager();
            }
            return s_dbMgr;
        }

        public void OpenDB(string path)
        {
            m_db = new DoxygenDB.DoxygenDB();
            m_db.Open(path);

            _OnOpen();
        }

        public void AnalysisDB()
        {
            m_db.Analyze();

            _OnOpen();
        }

        public DoxygenDB.DoxygenDB GetDB()
        {
            return m_db;
        }

        void _OnOpen()
        {
            var scene = UIManager.Instance().GetScene();
            scene.OnOpenDB();

            var mainUI = UIManager.Instance().GetMainUI();
        }
    }
}
