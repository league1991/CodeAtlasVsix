using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows.Input;

namespace CodeAtlasVSIX.Commands
{
    internal sealed class VSMenuCommand
    {
        public int CommandId = 0x0103;        
        public static readonly Guid CommandSet = new Guid("69c47ed5-456c-45d2-8d14-c3dbbce248b3");        
        private readonly Package package;
        private ExecutedRoutedEventHandler m_handler;

        private VSMenuCommand(Package package, int commandID, ExecutedRoutedEventHandler handler)
        {
            this.package = package;
            this.CommandId = commandID;
            this.m_handler = handler;

            if (package == null)
            {
                return;
            }

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }
        
        public static VSMenuCommand Instance
        {
            get;
            private set;
        }

        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        public static void Initialize(Package package, int commandID, ExecutedRoutedEventHandler handler)
        {
            Instance = new VSMenuCommand(package, commandID, handler);
        }
        
        private void MenuItemCallback(object sender, EventArgs e)
        {
            m_handler(this, null);
        }
    }
}
