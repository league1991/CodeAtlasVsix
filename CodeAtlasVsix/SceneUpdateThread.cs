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
        CodeScene m_scene = null;
        int m_sleepTime = 300;
        Thread m_thread = null;

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

        }
    }
}
