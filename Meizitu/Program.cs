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

        static void Main(string[] args)
        {
            Console.WriteLine("欢迎~\t{0}", DateTime.Now);
            Console.WriteLine("\n输出环境信息：");
            Console.WriteLine("\t网站地址：{0}", UnityModule.WebSite);
            Console.WriteLine("\t目录地址：{0}", UnityModule.CatalogAddress);
            Console.WriteLine("\t数据仓库：{0}", UnityModule.DataBasePath);
            Console.WriteLine("\t内容目录：{0}", UnityModule.ContentDirectory);

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
                }
            }
            else
            {
                Console.WriteLine("数据库已存在：{0}", UnityModule.ContentDirectory);
            }

            Console.WriteLine("\n正在连接数据库...");
            if (UnityDBController.CreateConnection(UnityModule.DataBasePath))
            {
                Console.WriteLine("数据库连接创建成功：{0}", UnityDBController.DataBaseConnection.State.ToString());
            }
            else
            {
                Console.WriteLine("数据库连接创建失败！");
                ExitApplication(1);
            }

            foreach (Tuple<string, string, string> ArchiveLink in ScanCatalog(UnityModule.CatalogAddress))
            {
                //Console.WriteLine("扫描到文章：{0}", ArchiveLink.Item3);
            }

            ExitApplication(0);
        }

        /// <summary>
        /// 扫描文章目录
        /// </summary>
        /// <param name="CatalogLink">目录链接</param>
        /// <returns>文章链接</returns>
        private static IEnumerable<Tuple<string, string, string>> ScanCatalog(string CatalogLink)
        {
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
                Console.WriteLine("—————— <<< 扫描到年份目录：{0} >>> ——————", MatchByYear.Groups["PublishYear"].Value);

                CatalogString = MatchByYear.Groups["CatalogByYear"].Value;
                foreach (Match MatchByMonth in new Regex(CatalogByMonthPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Matches(CatalogString))
                {
                    Console.WriteLine("——— <<< 扫描到月份目录：{0} (共 {1} 组图) >>> ———", MatchByMonth.Groups["PublishMonth"].Value, MatchByMonth.Groups["ArchiveCount"].Value);
                    CatalogString = MatchByMonth.Groups["CatalogByMonth"].Value;

                    foreach (Match MatchByDay in new Regex(CatalogByDayPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Matches(CatalogString))
                    {
                        Console.WriteLine("扫描到文章：{0} : {1} ({2})", MatchByDay.Groups["PublishDay"].Value, MatchByDay.Groups["Title"].Value, MatchByDay.Groups["ArchiveLink"].Value);
                        yield return new Tuple<string, string, string>(MatchByDay.Groups["PublishDay"].Value, MatchByDay.Groups["Title"].Value, MatchByDay.Groups["ArchiveLink"].Value);
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
