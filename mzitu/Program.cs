using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace mzitu
{
    class Program
    {

        static List<string> ErrorArchiveLink = new List<string>();
        static List<string> ErrorImageLink = new List<string>();

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
            public int PublishYear;
            public int PublishMonth;
            public int PublishDay;
            public string ArchiveLink;
        }
        
        static void Main(string[] args)
        {
            //TODO: http://www.mzitu.com/zipai/

            Console.WriteLine("{0}\t欢迎~", DateTime.Now);

            do
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("\n亲爱的 {0} ，准备好进入新世界了吗 ？(YES)\n\t", Environment.UserName);
                Console.ForegroundColor = ConsoleColor.Red;
            } while (Console.ReadLine().Trim().ToUpper() != "YES");
            Console.Clear();

            ShowEnvironment();
            if (!CheckRepositories()) ExitApplication(1);
            if (!ConnectDatabase()) ExitApplication(2);

            DateTime MaxDate = DateTime.MinValue;
            object MaxDateObject = UnityDBController.ExecuteScalar("SELECT MAX(PublishDate) FROM CatalogBase ;");
            if (MaxDateObject != DBNull.Value) MaxDate = Convert.ToDateTime(MaxDateObject);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n当前数据库最晚文章记录：{0}", MaxDate.Date.ToString("yyyy-MM-dd"));
            //存储文章目录信息
            GetCatalog();

            if (UnityModule.NewArchiveCount > 0)
            {
                do
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("\n亲爱的 {0} ，要开始下载组图吗 ？(YES)\n\t", Environment.UserName);
                    Console.ForegroundColor = ConsoleColor.Red;
                } while (Console.ReadLine().Trim().ToUpper() != "YES");

                DownloadArchives(MaxDate);

                if (ErrorArchiveLink.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n出错误的文章链接：\n    {0}", string.Join("\n    ", ErrorArchiveLink));
                }

                if (ErrorImageLink.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n出错误的图像链接：\n    {0}", string.Join("\n    ", ErrorImageLink));
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n\t\t未发现更新的文章...");
                Console.WriteLine("—————— <<< Nothing to Download . >>> ——————");
            }

            ExitApplication(0);
        }

        /// <summary>
        /// 下载新文章
        /// </summary>
        /// <param name="MaxDate">开始日期</param>
        private static void DownloadArchives(DateTime MaxDate)
        {
            List<ArchiveModel> ArchivePackageList = new List<ArchiveModel>();
            Console.WriteLine("\n开始分析文章内容：\n");
            //针对日期下载文章
            using (DbDataAdapter CatalogAdapter = UnityDBController.ExecuteAdapter(
                $"SELECT * FROM CatalogBase WHERE PublishDate >= #{MaxDate.Date}# ;"
                ))
            /*
            using (DbDataAdapter CatalogAdapter = UnityDBController.ExecuteAdapter("SELECT * FROM CatalogBase"))
             */
            {
                DataTable CatalogTable = new DataTable();
                CatalogAdapter.Fill(CatalogTable);
                foreach (DataRow CatalogRow in CatalogTable.Rows)
                {
                    DateTime PublishDate = Convert.ToDateTime(CatalogRow["PublishDate"]);

                    ArchivePackageList.Add(new ArchiveModel()
                    {
                        ArchiveID = Convert.ToInt32(CatalogRow["ArchiveID"]),
                        ArchiveLink = CatalogRow["ArchiveLink"] as string,
                        Title = CatalogRow["Title"] as string,
                        PublishYear = PublishDate.Year,
                        PublishMonth = PublishDate.Month,
                        PublishDay = PublishDate.Day,
                    });
                }
                CatalogTable?.Clear();
                CatalogTable?.Dispose();
            }
            Parallel.ForEach(ArchivePackageList, new Action<ArchiveModel>((ArchivePackage) => {
                string ImagePath = string.Empty, ArchiveDirectory = string.Empty, TempTitle = ArchivePackage.Title;
                TempTitle = TempTitle.Replace("?", "_w").Replace(":", "_m").Replace("\\", "_").Replace("/", "_f").Replace("|", "_s");
                ArchiveDirectory = Path.Combine(UnityModule.ContentDirectory, TempTitle);
                try
                {
                    if (!Directory.Exists(ArchiveDirectory))
                    {
                        Console.WriteLine("创建新目录：{0}", ArchiveDirectory);
                        Directory.CreateDirectory(ArchiveDirectory);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Print(ArchiveDirectory);
                    Console.WriteLine("创建文章目录失败：{0} / {1}", ArchiveDirectory, ex.Message);
                }
                UnityDBController.ExecuteNonQuery("DELETE FROM ImageBase WHERE ArchiveID = {0} ;", ArchivePackage.ArchiveID);
                foreach (string ImageLink in ScanArchive(ArchivePackage.ArchiveLink))
                {
                    ImagePath = Path.Combine(ArchiveDirectory, Path.GetFileName(ImageLink));

                    if (UnityDBController.ExecuteNonQuery("INSERT INTO ImageBase (ArchiveID, ImageLink, ImagePath) VALUES({0}, '{1}', '{2}') ;",
                        ArchivePackage.ArchiveID, ImageLink, ImagePath))
                    {
                        if (!File.Exists(ImagePath))
                        {
                            using (WebClient DownloadWebClient = new WebClient() { Encoding = Encoding.UTF8 })
                            {
                                try
                                {
                                    //绕过防盗链（使用 Fiddler4 对比盗链和非盗链的HTTP请求头信息即可）
                                    DownloadWebClient.Headers.Add(HttpRequestHeader.Referer, ArchivePackage.ArchiveLink);
                                    DownloadWebClient.DownloadFile(ImageLink, ImagePath);
                                }
                                catch (Exception ex)
                                {

                                    UnityModule.DebugPrint("下载图像遇到错误：{0} / {1}", ImageLink, ex.Message);
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("下载图像遇到错误：{0} / {1}", ImageLink, ex.Message);
                                    lock (ErrorImageLink)
                                    {
                                        ErrorImageLink.Add(ImageLink);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("图像记录插入数据仓库失败：{0}", ImageLink);
                    }
                }

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("文章下载完成：{0}", ArchivePackage.Title);
            }));

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n\t\t全部文章下载完毕！！！{0}", DateTime.Now.ToString());
            Console.WriteLine("—————— <<< Download Finished . >>> ——————");
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
                    FileController.SaveResource(UnityResource.mzitu, UnityModule.DataBasePath);
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
        /// 扫描并存储目录
        /// </summary>
        private static void GetCatalog()
        {
            UnityModule.NewArchiveCount = 0;
            foreach (ArchiveModel ArchivePackage in ScanCatalog(UnityModule.CatalogAddress))
            {
                if (Convert.ToInt32(UnityDBController.ExecuteScalar("SELECT COUNT(*) FROM CatalogBase WHERE ArchiveID = {0} ;", ArchivePackage.ArchiveID)) > 0)
                {
                    //Console.ForegroundColor = ConsoleColor.Gray;
                    //Console.WriteLine("已存在的文章：{0}", ArchivePackage.Title);
                }
                else
                {
                    if (UnityDBController.ExecuteNonQuery("INSERT INTO CatalogBase (ArchiveID, Title, PublishDate, ArchiveLink) VALUES({0}, '{1}', #{2}#, '{3}') ;",
                        ArchivePackage.ArchiveID,
                        ArchivePackage.Title,
                        string.Format("{0}/{1}/{2}",  ArchivePackage.PublishYear, ArchivePackage.PublishMonth, ArchivePackage.PublishDay),
                        ArchivePackage.ArchiveLink))
                    {
                        UnityModule.NewArchiveCount++;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("记录新文章信息：{0}", ArchivePackage.Title);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("文章 {0}({1}/{2}/{3}) 记录插入数据仓库失败！",
                            ArchivePackage.Title,
                            ArchivePackage.PublishYear,
                            ArchivePackage.PublishMonth,
                            ArchivePackage.PublishDay);
                    }
                }
            }
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

            string CatalogByYearPattern = "<div class=\"year\">(?<PublishYear>.+?)年</div><ul class=\"archives\">(?<CatalogByYear>.+?)</ul>";
            string CatalogByMonthPattern = "<li><p class=\"month\"><em>(?<PublishMonth>.+?)月</em> \\((?<ArchiveCount>.+?)组妹子图 \\)</p>(?<CatalogByMonth>.+?)</li>";
            string CatalogByDayPattern = ">(?<PublishDay>.+?)日: <a href=\"(?<ArchiveLink>.+?)\".*?>(?<Title>.+?)</a>";
            int TempArchiveID = 0, TempArchiveCount = 0;
            string TempArchiveLink = string.Empty, TempTitle = string.Empty;
            int TempPublishYear = 0, TempPublishMonth = 0, TempPublishDay = 0;
            foreach (Match MatchByYear in new Regex(CatalogByYearPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Matches(CatalogString))
            {
                TempPublishYear = Convert.ToInt32(MatchByYear.Groups["PublishYear"].Value);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("<<< 扫描到年份目录：{0} >>>", TempPublishYear);

                CatalogString = MatchByYear.Groups["CatalogByYear"].Value;
                foreach (Match MatchByMonth in new Regex(CatalogByMonthPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Matches(CatalogString))
                {
                    TempPublishMonth = Convert.ToInt32(MatchByMonth.Groups["PublishMonth"].Value);
                    TempArchiveCount = int.Parse(MatchByMonth.Groups["ArchiveCount"].Value);

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("    << 扫描到月份目录：{0} (共 {1} 组图) >>", TempPublishMonth, TempArchiveCount);
                    CatalogString = MatchByMonth.Groups["CatalogByMonth"].Value;

                    foreach (Match MatchByDay in new Regex(CatalogByDayPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Matches(CatalogString))
                    {
                        TempArchiveID = Convert.ToInt32(Path.GetFileName(MatchByDay.Groups["ArchiveLink"].Value));
                        TempTitle = MatchByDay.Groups["Title"].Value;
                        TempPublishDay = Convert.ToInt32(MatchByDay.Groups["PublishDay"].Value);
                        TempArchiveLink = MatchByDay.Groups["ArchiveLink"].Value;

                        //Console.ForegroundColor = ConsoleColor.Yellow;
                        //Console.WriteLine("        < 扫描到文章信息：{0} - {1} >", MatchByDay.Groups["PublishDay"].Value, MatchByDay.Groups["Title"].Value);
                        yield return new ArchiveModel()
                        {
                            ArchiveID = TempArchiveID,
                            ArchiveLink = TempArchiveLink,
                            Title = TempTitle,
                            PublishYear = TempPublishYear,
                            PublishMonth = TempPublishMonth,
                            PublishDay = TempPublishDay,
                        };
                    }
                }
            }

            yield break;
        }

        /// <summary>
        /// 扫描文章内容
        /// </summary>
        /// <param name="ArchiveLink">文章链接</param>
        /// <returns>内容信息</returns>
        private static IEnumerable<string> ScanArchive(string ArchiveLink)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("开始扫描文章：{0}", ArchiveLink);

            int TempArchiveID = Convert.ToInt32(Path.GetFileName(ArchiveLink));
            string ArchivePageLink = string.Empty, ArchiveString = string.Empty;
            string ImagePattern = "<div class=\"main-image\"><p><a href=\"(?<NextPageLink>.+?)\" ><img src=\"(?<ImageLink>.+?)\".*?/></a></p>";
            Queue<string> ArchiveLinkQueue = new Queue<string>();
            Match ArchiveMatch = null;

            ArchiveLinkQueue.Enqueue(ArchiveLink);
            while (ArchiveLinkQueue.Count > 0) 
            {
                int ErrorTime = 0;
                ArchivePageLink = ArchiveLinkQueue.Dequeue();
                //UnityModule.DebugPrint("链接出队：{0}", ArchivePageLink);

                do
                {
                    if (ErrorTime ++> 0) Thread.Sleep(1000);
                    ArchiveString = GetHTML(ArchivePageLink);
                }
                while (string.IsNullOrEmpty(ArchiveString) && ErrorTime <20);
                if (string.IsNullOrEmpty(ArchiveString))
                {
                    UnityModule.DebugPrint("下载页面失败多次，已跳过：{0}", ArchivePageLink);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("下载页面失败多次，已跳过：{0}", ArchivePageLink);
                    lock (ErrorArchiveLink)
                    {
                        ErrorArchiveLink.Add(ArchivePageLink);
                    }
                    continue;
                }

                ArchiveMatch = new Regex(ImagePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Match(ArchiveString);
                ArchivePageLink = ArchiveMatch.Groups["NextPageLink"].Value as string;
                //一组照片的最后一张的链接会指向另一组照片
                ArchiveLinkQueue.Enqueue(ArchivePageLink);
                yield return ArchiveMatch.Groups["ImageLink"].Value as string;

                if (!ArchivePageLink.StartsWith(ArchiveLink)) yield break;
                //UnityModule.DebugPrint("发现新链接：{0}", ArchivePageLink);
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
                UnityModule.DebugPrint("获取网页内容遇到异常：{0}", ex.Message);
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
            if(ExitCode==0) Process.Start("explorer.exe", UnityModule.ContentDirectory);
        }

    }
}
