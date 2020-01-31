using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CodeAtlasVSIX
{
    class DBManager
    {
        static DBManager s_dbMgr = null;
        //static int s_atlasPort = 12346;
        //static int s_sublimePort = 12345;

        DoxygenDB.DoxygenDB m_db;
        bool m_isBigSolution = false;

        void FindSolutionScale(string path)
        {
            if (path.Contains("Result_dummy.graph") || path.Contains("Result_files.graph"))
            {
                m_isBigSolution = true;
                return;
            }

            m_isBigSolution = false;
            var counter = new ProjectCounter();
            counter.Traverse();

            int nProjs = counter.GetTotalProjects();
            int nProjItems = counter.GetTotalProjectItems();

            // Use simple navigation mode for big solution
            if (nProjItems > 5000 || nProjs > 100)
            {
                m_isBigSolution = true;
            }
            else
            {
                m_isBigSolution = false;
            }
        }

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
            if (!File.Exists(path))
            {
                MessageBox.Show("Database file doesn't exist.", "Open Database");
                return;
            }

            FindSolutionScale(path);

            m_db = new DoxygenDB.DoxygenDB();
            m_db.Open(path, m_isBigSolution);

            _OnOpen();
        }

        public bool IsBigSolution()
        {
            return m_isBigSolution;
        }

        public void CloseDB()
        {
            _OnClose();
            m_db.Close();
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
        
        void _OnClose()
        {
            var scene = UIManager.Instance().GetScene();
            scene.OnCloseDB();
        }
    }
}
