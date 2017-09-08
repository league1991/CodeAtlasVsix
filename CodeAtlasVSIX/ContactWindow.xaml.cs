using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// Interaction logic for ContactWindow.xaml
    /// </summary>
    public partial class ContactWindow : UserControl
    {
        public ContactWindow()
        {
            InitializeComponent();
        }

        private void goToMarketPlaceButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://marketplace.visualstudio.com/items?itemName=YaobinOuyang.CodeAtlas#review-details");
        }

        private void shareToFacebook_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.facebook.com/sharer/sharer.php?u=https%3A%2F%2Fmarketplace.visualstudio.com%2Fitems%3FitemName%3DYaobinOuyang.CodeAtlas");
        }

        private void shareToTwitter_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.twitter.com/home?status=Just%20discovered%20this%20on%20the%20%23VSMarketplace%3A%20https%3A%2F%2Fmarketplace.visualstudio.com%2Fitems%3FitemName%3DYaobinOuyang.CodeAtlas");
        }

        private void shareToWeibo_Click(object sender, RoutedEventArgs e)
        {
            string url = "http://service.weibo.com/share/share.php?url=http://sina.lt/fcaX&title=这个visual studio插件可以查看C%2B%2B、C%23、Python的代码关系图，用来看代码挺方便的，快去试试吧，它是免费的哦~&pic=https://yaobinouyang.gallerycdn.vsassets.io/extensions/yaobinouyang/codeatlas/1.0.4/1504837533015/273052/1/main.png";
            System.Diagnostics.Process.Start(url);
        }

        private void goToGithub_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/league1991/CodeAtlasVsix");
        }
    }
}
