using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeAtlasVSIX
{
    class SceneUpdateThread
    {
        class ItemData
        {

        }

        CodeScene m_scene = null;
        int m_sleepTime = 300;
        Thread m_thread = null;
        bool m_isActive = true;
        Dictionary<string, ItemData> m_itemSet = new Dictionary<string, ItemData>();
        int m_edgeNum = 0;

        public SceneUpdateThread(CodeScene scene)
        {
            m_scene = scene;
            m_thread = new Thread(new ThreadStart(Run));
        }

        public void Start()
        {
            m_thread.Start();
        }

        void Run()
        {
            while(true)
            {
                if(m_isActive)
                {
                    var itemDict = m_scene.GetItemDict();
                    if(m_itemSet.Count != itemDict.Count || m_itemSet.Keys.SequenceEqual(itemDict.Keys))
                    {
                        UpdateLayeredLayoutWithComp();
                    }
                    MoveItems();
                    System.Console.Write("running\n");
                }
                Thread.Sleep(m_sleepTime);
            }
        }

        void UpdateLayeredLayoutWithComp()
        {

        }

        void MoveItems()
        {

        }

        void UpdateCallOrder()
        {

        }


    }
}
