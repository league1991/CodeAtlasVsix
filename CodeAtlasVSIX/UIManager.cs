using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAtlasVSIX
{
    class UIManager
    {
        CodeScene scene = null;
        MainUI mainUI = null;
        static UIManager uiMgr = null;

        public UIManager()
        {
            scene = new CodeScene();
        }

        public static UIManager Instance()
        {
            if (uiMgr != null)
            {
                uiMgr = new UIManager();
            }
            return uiMgr;
        }

        public MainUI GetMainUI()
        {
            return mainUI;
        }

        public CodeScene GetScene()
        {
            return scene;
        }
        
    }
}
