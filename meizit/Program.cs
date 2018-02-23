using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace meizit
{
    class Program
    {
        /// <summary>
        /// 开始文章ID
        /// </summary>
        private static readonly int StartPageIndex = 234;
        /// <summary>
        /// 主页地址
        /// </summary>
        private static readonly string HomeAddress = "http://www.meizit.com";
        /// <summary>
        /// 缓存路径
        /// </summary>
        private static readonly string DownloadDirectory = Path.Combine(Environment.CurrentDirectory, "meizit");

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("下载路径：{0}", DownloadDirectory);
            if (!TryCreateDirectory(DownloadDirectory))
            {
                Console.WriteLine("程序即将退出...");
                Console.Read(); Environment.Exit(1);
            }

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\n开始任务ID : {0}", StartPageIndex);

            StartWork(GetPageLink(HomeAddress, StartPageIndex.ToString()));

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n任务结束：{0}\n程序即将退出...", DateTime.Now.ToString());
            Console.Read();
            Process.Start("explorer.exe", DownloadDirectory);
        }

        /// <summary>
        /// 组合指定ID的文章链接
        /// </summary>
        /// <param name="homeAddress">主页地址</param>
        /// <param name="startPageIndex">开始文章ID</param>
        /// <returns>文章链接</returns>
        private static string GetPageLink(string homeAddress, string startPageIndex)
        {
            return string.Format("{0}/one/{1}.html", homeAddress, startPageIndex);
        }

        /// <summary>
        /// 获取网页内容
        /// </summary>
        /// <param name="Pagelink">网页链接</param>
        /// <returns>网页内容</returns>
        private static string GetHTML(string Pagelink)
        {
            try
            {
                using (WebClient UnityWebClient = new WebClient() { Encoding = Encoding.UTF8 })
                {
                    UnityWebClient.BaseAddress = Pagelink;
                    return UnityWebClient.DownloadString(Pagelink);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("获取网页内容遇到异常：{0}", ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// 开始任务
        /// </summary>
        /// <param name="StartPageLink">开始文章链接</param>
        private static void StartWork(string StartPageLink)
        {
            string MainContentPattern = string.Format("<([a-z]+)(?:(?!class)[^<>])*class=([\"']?){0}\\2[^>]*>(?>(?<o><\\1[^>]*>)|(?<-o></\\1>)|(?:(?!</?\\1).))*(?(o)(?!))</\\1>", Regex.Escape("col-md-8"));
            string HeaderPattern = "<header>.*?<a href=\"\">(?<ArticleHeader>.+?)</a>.*?</header>";
            string PreviousLinkPattern = "<li class=\"previous\"><a href=\"/one/(?<ArticleID>.+?).html\">.*?上一页</a></li>";
            string ImagePattern = "src=\"(?<ImageLink>.+?)\".*?>";
            Queue<string> ArticleLinkQueue = new Queue<string>();

            ArticleLinkQueue.Enqueue(StartPageLink);
            while (ArticleLinkQueue.Count > 0)
            {
                List<string> ImageLinks = new List<string>();
                string ArticlePageLink = string.Empty, ArticleID = String.Empty, ArticleString = string.Empty, ArticleDirectory = string.Empty, ArticleHeader = string.Empty, ImageLink = string.Empty;
                int ErrorTime = 0;
                ArticlePageLink = ArticleLinkQueue.Dequeue();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("{0}\n开始任务 : {1}", DateTime.Now.ToString(), ArticlePageLink);
                Debug.Print("开始任务：{0}", ArticlePageLink);
                do
                {
                    if (ErrorTime++ > 0) Thread.Sleep(1000);
                    ArticleString = GetHTML(ArticlePageLink);
                }
                while (string.IsNullOrEmpty(ArticleString) && ErrorTime < 10);

                if (string.IsNullOrEmpty(ArticleString))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("下载页面失败多次，已跳过：{0}", ArticlePageLink);
                    Debug.Print("下载HTML失败的链接：{0}", ArticlePageLink);
                    continue;
                }

                ArticleString = new Regex(MainContentPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Match(ArticleString).Value;
                if (string.IsNullOrEmpty(ArticleString))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("页面中未匹配到文章class：{0}", ArticlePageLink);
                    continue;
                }

                ArticleHeader= new Regex(HeaderPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Match(ArticleString).Groups["ArticleHeader"].Value;
                ArticleHeader = ArticleHeader.Trim().Replace("?", "_w").Replace(":", "_m").Replace("\\", "_").Replace("/", "_f").Replace("|", "_s").Replace("*", "_x");
                ArticleDirectory = Path.Combine(DownloadDirectory, ArticleHeader);
                if (!TryCreateDirectory(ArticleDirectory)) continue;

                ArticleID = new Regex(PreviousLinkPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Match(ArticleString).Groups["ArticleID"].Value;
                if (!string.IsNullOrEmpty(ArticleID))
                {
                    ArticlePageLink = GetPageLink(HomeAddress, ArticleID);
                    ArticleLinkQueue.Enqueue(ArticlePageLink);
                }

                foreach (string ImgLabel in Regex.Split(ArticleString, "<img"))
                {
                    ImageLink = new Regex(ImagePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Match(ImgLabel).Groups["ImageLink"].Value;
                    if (string.IsNullOrEmpty(ImageLink)) continue;
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("发现图像链接：{0}", ImageLink);
                    ImageLinks.Add(ImageLink);

                }
                Console.WriteLine("\t>>> 开始下载图像 ...");
                DownloadImages(ImageLinks, ArticleDirectory);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("———— <<< 文章下载完成：{0} >>> ————\n", ArticleHeader);
            }
        }

        /// <summary>
        /// 尝试创建目录
        /// </summary>
        /// <param name="TargetDirectory">目标目录</param>
        /// <returns></returns>
        private static bool TryCreateDirectory(string TargetDirectory)
        {
            if (!Directory.Exists(TargetDirectory))
            {
                try
                {
                    Directory.CreateDirectory(TargetDirectory);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("创建目录遇到异常：{0} / {1}",TargetDirectory, ex.Message);
                    Debug.Print("创建目录失败：{0} / {1}", TargetDirectory, ex.Message);
                    return false;
                }
            }
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("目录准备完成：{0}", TargetDirectory);
            return true;
        }

        /// <summary>
        /// 下载图像列表
        /// </summary>
        /// <param name="ImageLink">图像链接列表</param>
        /// <param name="ArticleDirectory">文章下载目录</param>
        private static void DownloadImages(List<string> ImageLinks, string ArticleDirectory)
        {
            Parallel.ForEach(ImageLinks, new Action<string>(ImageLink => 
            {
                string ImagePath = Path.Combine(ArticleDirectory, Path.GetFileName(ImageLink));
                int ErrorTime = 0;
                if (!File.Exists(ImagePath))
                {
                    do
                    {
                        if (ErrorTime++ > 0) Thread.Sleep(1000);
                    }
                    while (!DownloadImage(ImageLink, ImagePath) && ErrorTime < 10);

                    if (File.Exists(ImagePath))
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("图像下载成功：{0}", ImageLink);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("下载图像遇到错误：{0}", ImageLink);
                        Debug.Print("失败的图像链接：{0}", ImageLink);
                    }   
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("图像已经存在：{0}", ImageLink);
                }
            }));
        }

        /// <summary>
        /// 下载图像
        /// </summary>
        /// <param name="ImageLink">图像链接</param>
        /// <param name="ImagePath">图像路径</param>
        /// <returns></returns>
        private static bool DownloadImage(string ImageLink, string ImagePath)
        {
            using (WebClient DownloadWebClient = new WebClient() { Encoding = Encoding.UTF8 })
            {
                try
                {
                    DownloadWebClient.DownloadFile(ImageLink, ImagePath);
                    return true;
                }
                catch (Exception)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    return false;
                }
            }
        }

    }
}
