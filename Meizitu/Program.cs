using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Meizitu
{
    class Program
    {
        /// <summary>
        /// 全局数据库连接
        /// </summary>
        private static DataBaseController UnityDBController = new DataBaseController();

        /// <summary>
        /// 文章信息
        /// </summary>
        private struct ArchiveModel
        {
            public int ArchiveID;
            public string Title;
            public string PublishYear;
            public string PublishMonth;
            public string PublishDay;
            public string ArchiveLink;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("欢迎~\t{0}", DateTime.Now);

            ShowEnvironment();
            if (!CheckRepositories()) ExitApplication(1);
            if (!ConnectDatabase()) ExitApplication(2);

            foreach (ArchiveModel ArchiveLink in ScanCatalog(UnityModule.CatalogAddress))
            {
                //Console.WriteLine("扫描到文章：{0}", ArchiveLink.Item3);
            }

            ExitApplication(0);
        }

        /// <summary>
        /// 输出环境信息
        /// </summary>
        private static void ShowEnvironment()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n输出环境信息：");
            Console.WriteLine("\t网站地址：{0}", UnityModule.WebSite);
            Console.WriteLine("\t目录地址：{0}", UnityModule.CatalogAddress);
            Console.WriteLine("\t数据仓库：{0}", UnityModule.DataBasePath);
            Console.WriteLine("\t内容目录：{0}", UnityModule.ContentDirectory);
        }

        /// <summary>
        /// 检查存储仓库
        /// </summary>
        /// <returns>仓库是否可用</returns>
        private static bool CheckRepositories()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n检查内容目录...");
            if (!Directory.Exists(UnityModule.ContentDirectory))
            {
                try
                {
                    Console.WriteLine("创建内容目录：{0}", UnityModule.ContentDirectory);
                    Directory.CreateDirectory(UnityModule.ContentDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("创建内容目录遇到异常：{0}", ex.Message);
                    return false;
                }
            }
            else
            {
                Console.WriteLine("内容目录已存在：{0}", UnityModule.ContentDirectory);
            }

            Console.WriteLine("\n检查数据库...");
            if (!File.Exists(UnityModule.DataBasePath))
            {
                try
                {
                    Console.WriteLine("生成数据库：{0}", UnityModule.DataBasePath);
                    FileController.SaveResource(UnityResource.Meizitu, UnityModule.DataBasePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("生成数据库遇到异常：{0}", ex.Message);
                    return false;
                }
            }
            else
            {
                Console.WriteLine("数据库已存在：{0}", UnityModule.DataBasePath);
            }

            return true;
        }

        /// <summary>
        /// 连接数据库
        /// </summary>
        /// <returns>是否连接成功</returns>
        private static bool ConnectDatabase()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\n正在连接数据库...");
            if (UnityDBController.CreateConnection(UnityModule.DataBasePath))
            {
                Console.WriteLine("数据库连接创建成功：{0}", UnityDBController.DataBaseConnection.State.ToString());
            }
            else
            {
                Console.WriteLine("数据库连接创建失败！");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 扫描文章目录
        /// </summary>
        /// <param name="CatalogLink">目录链接</param>
        /// <returns>文章链接</returns>
        private static IEnumerable<ArchiveModel> ScanCatalog(string CatalogLink)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n开始扫描目录：{0}\n", CatalogLink);

            string CatalogString = GetHTML(CatalogLink);
            if (string.IsNullOrEmpty(CatalogString)) yield break;

            string CatalogPattern = string.Format("<([a-z]+)(?:(?!class)[^<>])*class=([\"']?){0}\\2[^>]*>(?>(?<o><\\1[^>]*>)|(?<-o></\\1>)|(?:(?!</?\\1).))*(?(o)(?!))</\\1>", 
                Regex.Escape("all"));
            CatalogString = new Regex(CatalogPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Match(CatalogString).Value;
            if (string.IsNullOrEmpty(CatalogString)) yield break;

            string CatalogByYearPattern = "<div class=\"year\">(?<PublishYear>.+?)</div><ul class=\"archives\">(?<CatalogByYear>.+?)</ul>";
            string CatalogByMonthPattern = "<li><p class=\"month\"><em>(?<PublishMonth>.+?)</em> \\((?<ArchiveCount>.+?)组妹子图 \\)</p>(?<CatalogByMonth>.+?)</li>";
            string CatalogByDayPattern = ">(?<PublishDay>.+?): <a href=\"(?<ArchiveLink>.+?)\".*?>(?<Title>.+?)</a>";

            foreach (Match MatchByYear in new Regex(CatalogByYearPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Matches(CatalogString))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("<<< 扫描到年份目录：{0} >>>", MatchByYear.Groups["PublishYear"].Value);

                CatalogString = MatchByYear.Groups["CatalogByYear"].Value;
                foreach (Match MatchByMonth in new Regex(CatalogByMonthPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Matches(CatalogString))
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("    << 扫描到月份目录：{0} (共 {1} 组图) >>", MatchByMonth.Groups["PublishMonth"].Value, MatchByMonth.Groups["ArchiveCount"].Value);
                    CatalogString = MatchByMonth.Groups["CatalogByMonth"].Value;

                    foreach (Match MatchByDay in new Regex(CatalogByDayPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Matches(CatalogString))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("        文章：{0} - {1}", MatchByDay.Groups["PublishDay"].Value, MatchByDay.Groups["Title"].Value);
                        yield return new ArchiveModel()
                        {
                            ArchiveID = Convert.ToInt32(Path.GetFileName(MatchByDay.Groups["ArchiveLink"].Value)),
                            ArchiveLink = MatchByDay.Groups["ArchiveLink"].Value,
                            Title = MatchByDay.Groups["Title"].Value,
                            PublishYear = MatchByYear.Groups["CatalogByYear"].Value,
                            PublishMonth = MatchByMonth.Groups["CatalogByMonth"].Value,
                            PublishDay = MatchByDay.Groups["PublishDay"].Value,
                        };
                    }
                }
            }

            yield break;
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
            } catch(Exception ex)
            {
                Console.WriteLine("获取网页内容遇到异常：{0}", ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// 退出应用程序
        /// </summary>
        private static void ExitApplication(int ExitCode)
        {
            Console.WriteLine("\n应用程序即将退出...");
            UnityDBController?.CloseConnection();
            Console.Read();
        }
    }
}
