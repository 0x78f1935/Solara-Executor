﻿using System.Collections.Concurrent;
using CommandLine;
using System.Text.RegularExpressions;
using System.Net;
using System.Diagnostics;
using System.Xml;


namespace RetroDownloader
{
    #region Parallel Worker
    public static class ParallelExtention
    {
        public static IEnumerable<IEnumerable<T>> GetParrallelConsumingEnumerable<T>(this IProducerConsumerCollection<T> collection)
        {
            T item;
            while (collection.TryTake(out item))
            {
                yield return GetParrallelConsumingEnumerableInner(collection, item);
            }
        }

        private static IEnumerable<T> GetParrallelConsumingEnumerableInner<T>(IProducerConsumerCollection<T> collection, T item)
        {
            yield return item;
            while (collection.TryTake(out item))
            {
                yield return item;
            }
        }
    }
    #endregion

    #region Application
    public class Application
    {
        public static bool isRunning = true;  // Indicator if the application is running

        #region Argument Parser Variables
        private bool debug;  //  Verbose
        private string outputPath;  // Out
        private string buildVersion;  // Build
        private string agent; // Agent
        private int maxConcurrentWorkers; // Workers
        private bool downloadAll; // Download all indicator
        #endregion

        #region Uri(s)
        private string urlExternalvars = "https://habbo.com/gamedata/external_variables/1";
        private string urlExternaltext = "https://habbo.com/gamedata/external_flash_texts/1";
        private string urlProductdata = "https://habbo.com/gamedata/productdata/1";
        private string urlFurnidataTXT = "https://habbo.com/gamedata/furnidata/1";
        private string urlFurnidataXML = "https://habbo.com/gamedata/furnidata_xml/1";
        private string urlFigureData = "http://habbo.com/gamedata/figuredata/1";
        private string urlFurniture = "http://images.habbo.com/dcr/hof_furni";
        private string urlCatalogicon = "https://images.habbo.com/c_images/catalogue/icon_";
        private string urlSoundmachine = "https://images.habbo.com/dcr/hof_furni/mp3/sound_machine_sample_";
        private string urlBadges = "http://images.habbo.com/c_images/album1584";
        private string urlGordon = "https://images.habbo.com/gordon";
        private string urlQuests = "https://images.habbo.com/c_images/Quests";
        private string urlEffectMap;  // Will be set at  runtime
        private string urlAvatarActions;  // Will be set at runtime
        private string urlFigureMap; // Will be set at runtime
        private string urlFigureMapV2;  // Will be set at runtime
        #endregion

        #region Static Variables
        private int total_downloads = 0;  // Holds total downloaded files
        private Thread ThreadDownloaderMaster;  // Holds thread which we use for downloading files
        private Thread ThreadDownloaderSlave;  // Holds thread which we use for downloading files
        private Thread ThreadDownloaderSuperSlave;  // Holds thread which we use for downloading files
        private Thread ThreadDiscovery;   // This thread is used to discover things such as icons
        private bool _cred;
        private string magic = @"(\${[a-zA-Z.]+}|((http|https):\/\/)|[a-z_A-Z]+/|/)[a-zA-Z._0-9/]+(\.json|\.xml|\.swf|\.flv|\.png|\.jpeg|\.jpg|\.gif|\.bmp|\.ico|\.tiff|\.tif|\.svg|\.otf|\.ttf|\.woff|\.woff2|\.mp3|\.wav|\.wma|\.m4a|\.mp4|\.webm|\.avi|\.wmv|\.mov|\.mpg|\.mpeg|\.3gp|\.mkv|\.txt|\.csv|\.tsv|\.gif)";
        #endregion

        #region Application utilities
        private ConcurrentQueue<string> QueueDownloads = new ConcurrentQueue<string>();  // Holds items which are going to be downloaded
        private Stopwatch timer = new Stopwatch();  // Tracks execution time of application
        private string embeddir;  // Holds additional subdirs
        #endregion

        #region Do Section
        // This section gets ignored if downloadAll is True
        private bool doArticles;  // Download articles
        private bool doBadges;  // Download badges
        private bool doClothing; // Download clothing
        private bool doEffects;  // Download effects
        private bool doFurniture;  // Download furniture
        private bool doGamedata;  // Download gamedata
        private bool doGordon;  // Download gordon
        private bool doHotelView;  // Download Hotel view
        private bool doParts;  // Download Parts
        private bool doPets; // Download pets
        private bool doSound;  // Download sounds
        private bool doQuests;  // Download Quests
        private bool doArchive; // Download Archive
        private bool doCatalog; // Download catel
        #endregion

        #region Application arguments
        private class Options
        {
            #region Application Arguments
            [Option('v', "verbose", Required = false, HelpText = "Output debug stdout information.", Default = false)]
            public bool Verbose { get; set; }
            [Option('o', "out", Required = false, HelpText = "Set the output folder.", Default = ".")]
            public string Out { get; set; }
            [Option('b', "build", Required = false, HelpText = "Build version of Game, found at https://habboassets.com/swfs.", Default = "latest")]
            public string Build { get; set; }
            [Option('a', "agent", Required = false, HelpText = "Set custom user agent.", Default = "Mozilla/5.0 (Windows; U; Windows NT 6.2) AppleWebKit/534.2.1 (KHTML, like Gecko) Chrome/35.0.822.0 Safari/534.2.1")]
            public string Agent { get; set; }
            [Option('w', "workers", Required = false, HelpText = "Total concurrent downloaders used for downloading data.", Default = 2)]
            public int Workers { get; set; }
            [Option('e', "embeddir", Required = false, HelpText = "Manipulate the embedded directory. Delimiter: \";\". See Readme.", Default = "")]
            public string embeddir { get; set; }
            #endregion

            #region Do Section
            [Option('R', "articles", Required =false, HelpText = "Download Articles.")]
            public bool doArticles { get; set; }
            [Option('B', "badges", Required = false, HelpText = "Download Badges.")]
            public bool doBadges { get; set; }
            [Option('C', "clothing", Required = false, HelpText = "Download Clothing.")]
            public bool doClothing { get; set; }
            [Option('E', "effects", Required = false, HelpText = "Download effects.")]
            public bool doEffects { get; set; }
            [Option('F', "furniture", Required = false, HelpText = "Download Furniture.")]
            public bool doFurniture { get; set; }
            [Option('O', "gordon", Required = false, HelpText = "Download gordon data.")]
            public bool doGordon { get; set; }
            [Option('G', "gamedata", Required = false, HelpText = "Download gamedata.")]
            public bool doGamedata { get; set; }
            [Option('H', "hotelview", Required = false, HelpText = "Download hotelview.")]
            public bool doHotelView { get; set; }
            [Option('P', "parts", Required = false, HelpText = "Download Badgeparts.")]
            public bool doParts { get; set; }
            [Option('T', "pets", Required = false, HelpText = "Download Pets.")]
            public bool doPets { get; set; }
            [Option('S', "sound", Required = false, HelpText = "Download Sound.")]
            public bool doSound{ get; set; }
            [Option('Q', "quests", Required = false, HelpText = "Download Quests.")]
            public bool doQuests { get; set; }
            [Option('L', "catalog", Required = false, HelpText = "Download Catalog Icons.")]
            public bool doCatalog { get; set; }
            [Option('I', "archive", Required = false, HelpText = "Download Archive.")]
            public bool doArchive { get; set; }
            #endregion

            #region The I don't care Section
            [Option('A', "all", Required = false, HelpText = "Download All.")]
            public bool All { get; set; }
            #endregion
        }
        #endregion

        #region __init__
        public static void Main(string[] args)
        {// Entrypoint
            new Application(args);
        }

        public static void WrapperEntrypoint(
            bool debug,
            string outputPath,
            string buildVersion,
            string agent,
            int maxConcurrentWorkers,
            bool downloadAll,
            bool doArticles,
            bool doBadges,
            bool doClothing,
            bool doEffects,
            bool doFurniture,
            bool doGamedata,
            bool doGordon,
            bool doHotelView,
            bool doParts,
            bool doPets,
            bool doSound,
            bool doQuests,
            bool doArchive,
            bool doCatalog,
            string embeddir
        ) {// Wrapper Entrypoint
            string inputvalues = "";
            inputvalues += $"-o {outputPath} ";
            inputvalues += $"-b {buildVersion} ";
            inputvalues += $"-a {agent} ";
            inputvalues += $"-w {maxConcurrentWorkers} ";
            if (embeddir.Count() > 0) { inputvalues += $"-e {embeddir} "; }
            if (debug) { inputvalues += "-v "; }
            if (downloadAll) { inputvalues += "-A "; }
            else { 
                if (doArticles) { inputvalues += "-R "; }
                if (doBadges) { inputvalues += "-B "; }
                if (doClothing) { inputvalues += "-C "; }
                if (doEffects) { inputvalues += "-E "; }
                if (doFurniture) { inputvalues += "-F "; }
                if (doGordon) { inputvalues += "-O "; }
                if (doGamedata) { inputvalues += "-G "; }
                if (doHotelView) { inputvalues += "-H "; }
                if (doParts) { inputvalues += "-P "; }
                if (doPets) { inputvalues += "-T "; }
                if (doSound) { inputvalues += "-S "; }
                if (doQuests) { inputvalues += "-Q "; }
                if (doArchive) { inputvalues += "-I "; }
                if (doCatalog) { inputvalues += "-L "; }
            }
            new Application(inputvalues.Split(" "));
        }

        private Application(string[] args)
        {

            #region make sure arguments are provided
            if (args.Count() <= 0)
            {
                return;
            }
            #endregion

            # region Parse arguments
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(
            o =>
            {
                debug = o.Verbose;
                outputPath = o.Out;
                buildVersion = o.Build;
                agent = o.Agent;
                maxConcurrentWorkers = o.Workers;
                downloadAll = o.All;
                doArticles = o.doArticles;
                doBadges = o.doBadges;
                doClothing = o.doClothing;
                doEffects = o.doEffects;
                doFurniture = o.doFurniture;
                doGamedata= o.doGamedata;
                doGordon = o.doGordon;
                doHotelView = o.doHotelView;
                doParts = o.doParts;
                doPets = o.doPets;
                doSound = o.doSound;
                doQuests = o.doQuests;
                doArchive= o.doArchive;
                doCatalog = o.doCatalog;
                embeddir = o.embeddir;
            }
            );
            #endregion

            ThreadDownloaderMaster = new Thread(Downloader);
            ThreadDownloaderSlave = new Thread(Downloader);
            ThreadDownloaderSuperSlave = new Thread(Downloader);
            ThreadDiscovery = new Thread(Discovery);

            #region Obtain latest production version if latest has been set
            if (buildVersion == "latest")
            {
                latestBuildVersion();  // Obtains latest build version
            }
            #endregion

            #region Fix relative output path
            if (outputPath.StartsWith("./"))
            {
                outputPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, outputPath.Replace("./", ""));
            }
            else if (outputPath == ".") { 
                outputPath = System.AppDomain.CurrentDomain.BaseDirectory;
            }
            #endregion

            urlEffectMap = $"{urlGordon}/{buildVersion}/effectmap.xml";
            urlAvatarActions= $"{urlGordon}/{buildVersion}/HabboAvatarActions.xml";
            urlFigureMap = $"{urlGordon}/{buildVersion}/figuremap.xml";
            urlFigureMapV2= $"{urlGordon}/{buildVersion}/figuremapv2.xml";

            #region Start application
            _cred = false;
            timer.Start();
            Start();
            #endregion
        }
        #endregion

        private string request(string url)
        {
            #region Obtain source of provided url
            HttpWebRequest theRequest = (HttpWebRequest)WebRequest.Create(url);
            theRequest.Headers["user-agent"] = agent;
            theRequest.Method = "GET";
            try
            {
                WebResponse theResponse = theRequest.GetResponse();
                StreamReader sr = new StreamReader(theResponse.GetResponseStream(), System.Text.Encoding.UTF8);
                string result = sr.ReadToEnd();
                sr.Close();
                theResponse.Close();
                return result;
            }
            catch (WebException)
            {
                return "";
            }
            #endregion
        }

        public void Start()
        {
            #region Core of application
            isRunning = true;
            ThreadDiscovery.Start();
            ThreadDownloaderMaster.Start();
            ThreadDownloaderSlave.Start();
            ThreadDownloaderSuperSlave.Start();
            while (!QueueDownloads.IsEmpty && ThreadDiscovery.IsAlive)
            {
                ThreadDiscovery.Join();
            }
            #endregion
        }

        private string ParseFormat(string input)
        {
            #region Format input to valid URL
            if (input.Contains("${image.library.url}"))
            {
                return input.Replace("${image.library.url}", "https://images.habbo.com/c_images/");
            }
            else if (input.Contains("${flash.client.url}"))
            {
                return input.Replace("${flash.client.url}", "https://images.habbo.com/gordon/" + buildVersion + "/");
            }
            else if (input.StartsWith("src="))
            {
                return input.Split("\"")[1];
            }
            else
            {
                if (!input.StartsWith("http") && !input.StartsWith("/"))
                {
                    if (debug) { Console.WriteLine("|  RIP"); }
                }
            }
            return input;
            # endregion
        }

        private void AddToQueue(Uri uri, string output_location)
        {
            string filename = uri.AbsolutePath.Split("/").Reverse().First();
            string path = uri.AbsolutePath.Replace(filename, "");
            if (!path.StartsWith("/"))
            {
                if (debug) { Console.WriteLine("|  RIP"); }
            }

            if (!QueueDownloads.Contains($"{uri.AbsoluteUri};{output_location}")) { QueueDownloads.Enqueue($"{uri.AbsoluteUri};{output_location}"); }
        }

        private void ScrapeLink(Uri targetedLink)
        {
            #region Apply regex magic
            string source = request(targetedLink.ToString());

            try
            {
                foreach (Match match in Regex.Matches(source, magic, RegexOptions.None, TimeSpan.FromSeconds(1))) {
                    if (debug) { Console.WriteLine($"[***] Found '{match.Value}' at position {match.Index}"); }
                    if (match.Success) { 
                        Uri _uri = null;
                        string _match = match.ToString();
                        if (_match.StartsWith('/'))
                        {
                            if (targetedLink.ToString().Contains(".php"))
                            {
                                _uri = new Uri(ParseFormat($"{targetedLink.ToString().Replace(".php", "")}{_match}"));
                            }
                            else if (targetedLink.ToString().EndsWith("/")) {
                                _uri = new Uri(ParseFormat($"{targetedLink.ToString().Substring(0, targetedLink.ToString().Length - 1)}{_match}"));
                            }
                            else
                            {
                                _uri = new Uri(ParseFormat($"{targetedLink}{_match}"));
                            }
                        }
                        else {
                            if (!_match.StartsWith("http") && !_match.StartsWith("/")) {
                                if (!_match.Contains("web_promo_small") && !_match.StartsWith("$"))
                                {
                                    string _ = $"https://www.habborator.org/archive/{_match}";
                                    _uri = new Uri(ParseFormat(_));
                                }
                                else if (!_match.Contains("https://images.habbo.com/") && !_match.StartsWith("$"))
                                {
                                    string _ = $"https://images.habbo.com/c_images/{_match}";
                                    _uri = new Uri(ParseFormat(_));
                                }
                                else if (_match.StartsWith("$")) {
                                    _uri = new Uri(ParseFormat(_match));
                                }
                            }
                            else if (_match.StartsWith("http"))
                            {
                                _uri = new Uri(ParseFormat(_match));
                            }
                        }

                        if (_uri.ToString().Contains("habborator.org")) {
                            AddToQueue(_uri, "/c_images/archive/");
                        } else { 
                            AddToQueue(_uri, "");
                        }
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Do Nothing: Assume that timeout represents no match.
            }

            // Check for quests
            if ((doQuests || downloadAll) && targetedLink.ToString().Contains("external_flash_texts"))
            {

                try
                {
                    foreach (Match match in Regex.Matches(source, @"^quests\.[a-zA-Z0-9_]+\.[a-zA-Z0-9_]+", RegexOptions.None, TimeSpan.FromSeconds(1)))
                    {
                        if (debug) { Console.WriteLine($"[***] Found '{match.Value}' at position {match.Index}"); }
                        if (match.Success)
                        {
                            string[] quest_items = match.ToString().Split(".");
                            string fname = quest_items[2].Replace("_name", "");
                            fname = String.Join("_", fname.Split("_").SkipLast(1).ToArray());
                            string target_uri = $"{urlQuests}/{quest_items[1]}_{fname}.png";
                            target_uri = target_uri.Replace("_.png", ".png");
                            Uri uri_png = new Uri(ParseFormat(target_uri));
                            AddToQueue(uri_png, "");
                        }
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Do Nothing: Assume that timeout represents no match.
                }
            }


            //Check for badges
            if ((doBadges || downloadAll) && targetedLink.ToString().Contains("external_flash_texts"))
            {

                try
                {
                    foreach (Match match in Regex.Matches(source, @"^quests\.[a-zA-Z0-9_]+\.[a-zA-Z0-9_]+", RegexOptions.None, TimeSpan.FromSeconds(1)))
                    {
                        if (debug) { Console.WriteLine($"[***] Found '{match.Value}' at position {match.Index}"); }
                        if (match.Success)
                        {
                            string[] quest_items = match.ToString().Split(".");
                            string fname = quest_items[2].Replace("_name", "");
                            fname = String.Join("_", fname.Split("_").SkipLast(1).ToArray());
                            string target_uri = $"{urlQuests}/{quest_items[1]}_{fname}.png";
                            target_uri = target_uri.Replace("_.png", ".png");
                            Uri uri_png = new Uri(ParseFormat(target_uri));
                            AddToQueue(uri_png, "");
                        }
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Do Nothing: Assume that timeout represents no match.
                }
            }

            // Furniture
            if ((doFurniture || downloadAll) && targetedLink.ToString().Contains("furnidata/1"))
            {
                try
                {
                    foreach (Match match in Regex.Matches(source, @"([0-9a-zA-Z_]{4,}?[0-9]?(?<!\btrue|false\b))(\*{1}[0-9]+)?\""\,\""[0-9]+", RegexOptions.None, TimeSpan.FromSeconds(1)))
                    {
                        if (debug) { Console.WriteLine($"[***] Found '{match.Value}' at position {match.Index}"); }
                        if (match.Success)
                        {
                            string furni_id = match.ToString().Split($"\",\"")[1];
                            string furni_name = match.ToString().Split($"\",\"")[0];
                            string icon_path = "/dcr/hof_furni/icons/";
                            string swf_path = "/dcr/hof_furni/";

                            if (furni_name.Contains("*"))
                            {
                                string icon_name = furni_name.Replace("*", "_").Replace(".", "_");
                                string file_name = furni_name.Split("*")[0];
                                AddToQueue(new Uri(ParseFormat($"{urlFurniture}/{furni_id}/{icon_name}_icon.png")), icon_path);
                                AddToQueue(new Uri(ParseFormat($"{urlFurniture}/{furni_id}/{file_name}.swf")), swf_path);
                            }
                            else
                            {
                                AddToQueue(new Uri(ParseFormat($"{urlFurniture}/{furni_id}/{furni_name.Replace(".", "_")}.swf")), swf_path);
                                AddToQueue(new Uri(ParseFormat($"{urlFurniture}/{furni_id}/{furni_name.Replace(".", "_")}_icon.png")), icon_path);
                            }
                        }
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Do Nothing: Assume that timeout represents no match.
                }
            }

            // Hotel View
            if ((doHotelView || downloadAll) && targetedLink.ToString().Contains("external_variables"))
            {
                try
                {
                    foreach (Match match in Regex.Matches(source, @"(?<=landing\.view\.background.+=).+(?<=reception\/)(.+)", RegexOptions.None, TimeSpan.FromSeconds(1)))
                    {
                        if (debug) { Console.WriteLine($"[***] Found '{match.Value}' at position {match.Index}"); }
                        if (match.Success)
                        {
                            AddToQueue(new Uri(ParseFormat(match.ToString())), "/c_images/reception");
                        }
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Do Nothing: Assume that timeout represents no match.
                }
            }

            if ((doBadges || downloadAll) && targetedLink.ToString().Contains("external_flash_texts"))
            {
                foreach (string Line in source.Split(Environment.NewLine.ToCharArray()))
                {
                    //Check for badges
                    Match matchOne = Regex.Match(Line, @"^badge_(?:name|desc)_([^=]+)=");
                    Match matchTwo = Regex.Match(Line, @"^(.*)_badge_(?:name|desc).*=");
                    if (matchOne.Success || matchTwo.Success)
                    {
                        string badge;
                        if (matchTwo.Success)
                        {
                            badge = matchTwo.ToString().Replace("_badge_name=", "").Replace("_badge_desc=", "");
                        }
                        else
                        {
                            badge = matchOne.ToString().Replace("badge_desc_", "").Replace("badge_name_", "").Replace("=", "");
                        }
                        Uri uri_png = new Uri(ParseFormat($"{urlBadges}/{badge}.png"));
                        Uri uri_gif = new Uri(ParseFormat($"{urlBadges}/{badge}.gif"));
                        AddToQueue(uri_png, "/c_images/album1584/");
                        AddToQueue(uri_gif, "/c_images/album1584/");
                    }
                }
            }
            #endregion
        }

        private void Download(Uri uri, string overwrite_path)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers["user-agent"] = agent;

                string filename = uri.AbsolutePath.Split("/").Reverse().First();
                string path = uri.AbsolutePath.Replace(filename, "");

                if (path.StartsWith("/"))
                {
                    string output_path;
                    if (overwrite_path.Count() > 0 && overwrite_path.StartsWith("/"))
                    {
                        if (filename.Count() > 0) { 
                            overwrite_path = overwrite_path.Replace(filename, "");
                        }
                        output_path = $"{outputPath}{overwrite_path}".Replace('/', Path.DirectorySeparatorChar);
                    }
                    else { 
                        output_path = Path.Combine(outputPath, path.Substring(1));
                    }

                    //path = path.Split(".com").Reverse().First();
                    #region Create path if not exists
                    if (output_path.Contains("http")) {
                        Console.Write("ew");
                    }
                    String folder = Path.GetDirectoryName(output_path);
                    if (!Directory.Exists(folder))
                    {
                        // Try to create the directory.
                        Directory.CreateDirectory(folder);
                    }
                    #endregion
                    if (!System.IO.File.Exists(Path.Combine(output_path, filename)))
                    {
                        try
                        {
                            client.DownloadFile(uri, Path.Combine(output_path, filename));
                            if (debug) { Console.WriteLine($"|[200]|  DOWNLOADING: {uri}"); }
                            total_downloads += 1;
                        }
                        catch (Exception ex)
                        {
                            if (debug) { Console.WriteLine($"|[XXX]|  SKIPPING: {uri}"); }
                        }
                    }
                }
                else
                { // If this fires, we have a unknown path
                    if (debug) { Console.WriteLine("|  RIP"); }
                }
            }
        }

        private void Downloader()
        {  // Downloads Items from queue parallel
            while (isRunning || !QueueDownloads.IsEmpty || ThreadDiscovery.IsAlive)
            {
                Parallel.ForEach(
                    ParallelExtention.GetParrallelConsumingEnumerable(QueueDownloads),
                    new ParallelOptions { MaxDegreeOfParallelism = maxConcurrentWorkers },
                    Items =>
                    {
                        foreach (string target in Items)
                        {
                            string[] target_collection = target.Split(";");
                            Download(new Uri(target_collection[0]), target_collection[1]);
                        }
                    });
            }
            if (!_cred)
            {
                timer.Stop();
                _cred = true;
                Thread.Sleep(1000);
                Console.WriteLine("");
                Console.WriteLine("|+|Brought you by: Undeƒined -> https://github.com/0x78f1935");
                Console.WriteLine("|+|Inspired by: Habbo-Downloader -> https://github.com/higoka/habbo-downloader");
                Console.WriteLine("|+|Inspired by: All-in-one-converter -> https://git.camwijs.eu/duckietm/All-in-1-converter");
                Console.WriteLine("|+|Archive by: Habborator -> https://www.habborator.org/archive/index.php");
                Console.WriteLine("|+|Build for: CMS -> https://github.com/0x78f1935/Retro-CMS");
                Console.WriteLine($"|  Total Downloads: {total_downloads}");
                Console.WriteLine($"|  Running time: {timer.Elapsed}");
            }
        }

        private void IterativeAddDownloadQueue(Uri uri, string addition)
        {
            int canFail = 3;
            int totalFailed = 0;
            int matched = 0;
            while (totalFailed <= canFail)
            {
                Uri target = new Uri($"{uri}{matched}{addition}");
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(target);
                request.Method = "HEAD";
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            if (target.ToString().Contains(".mp3"))
                            {
                                AddToQueue(target, "/dcr/hof_furni/mp3/");
                            }
                            else if (uri.ToString() == urlCatalogicon)
                            {
                                AddToQueue(target, "/c_images/catalogue/");
                            }
                            else
                            {
                                AddToQueue(target, "");
                            }
                            totalFailed = 0;
                        }
                        else
                        {
                            totalFailed++;
                        }
                    }
                }
                catch
                {
                    totalFailed++;
                }
                matched++;
            }
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        private void Discovery()
        {
            if (doCatalog || downloadAll) { 
                IterativeAddDownloadQueue(new Uri(urlCatalogicon), ".png");
            }
            if (doSound || downloadAll) { 
                IterativeAddDownloadQueue(new Uri(urlSoundmachine), ".mp3");
            }

            #region Furniture
            if (doFurniture || downloadAll) { 
                XmlDocument xmlDoc = new XmlDocument();
                string xmlData = request(urlFurnidataXML);
                xmlDoc.LoadXml(xmlData);
                XmlNode root = xmlDoc.DocumentElement;
                foreach (XmlNode _ in root.ChildNodes)
                {
                    foreach (XmlNode furni in _.ChildNodes)
                    {
                        XmlNode _revision = furni.SelectSingleNode("revision");
                        string revision = _revision.FirstChild.Value;
                        string name = furni.Attributes["classname"].Value.ToString();
                        if (name.Contains("*"))
                        {
                            string icon_name = name.Replace("*", "_").Replace(".", "_");
                            string file_name = name.Split("*")[0];
                            AddToQueue(new Uri(ParseFormat($"{urlFurniture}/{revision}/{icon_name}_icon.png")), "/dcr/hof_furni/icons/");
                            AddToQueue(new Uri(ParseFormat($"{urlFurniture}/{revision}/{file_name}.swf")), "/dcr/hof_furni/");
                        }
                        else
                        {
                            AddToQueue(new Uri(ParseFormat($"{urlFurniture}/{revision}/{name.Replace(".", "_")}_icon.png")), "/dcr/hof_furni/icons/");
                            AddToQueue(new Uri(ParseFormat($"{urlFurniture}/{revision}/{name.Replace(".", "_")}.swf")), "/dcr/hof_furni/");
                        }
                    }
                }
                #region place furniture which habbo removed from the internet
                string[] path_combination = { System.AppDomain.CurrentDomain.BaseDirectory };
                foreach (string path in embeddir.Split(";"))
                {
                    path_combination = path_combination.Concat(new[] { path }).ToArray();
                }
                path_combination = path_combination.Concat(new[] { "Resources" }).ToArray();
                DirectoryInfo d = new DirectoryInfo(Path.Combine(path_combination));
                foreach (var file in d.GetFiles("*.swf"))
                {
                    try { 
                        File.Copy(file.FullName, $"{outputPath}/dcr/hof_furni/{file.Name}");
                        if (debug) { Console.WriteLine($"|[200]|  DOWNLOADING: {file}"); }
                    }
                    catch (System.IO.IOException) {
                        if (debug) { Console.WriteLine($"|[XXX]|  SKIPPING: {file}"); }
                    }
                }
                foreach (var file in d.GetFiles("*.png"))
                {
                    try { 
                        File.Copy(file.FullName, $"{outputPath}/dcr/hof_furni/icons/{file.Name.Replace(".png", "_icon.png")}");
                        if (debug) { Console.WriteLine($"|[200]|  DOWNLOADING: {file}"); }
                    }
                    catch (System.IO.IOException) {
                        if (debug) { Console.WriteLine($"|[XXX]|  SKIPPING: {file}"); }
                    }
                }
                if (debug) { Console.WriteLine($"|[XXX]|  COPYING LOST FILES"); }
                path_combination = path_combination.Concat(new[] { "default-assets" }).ToArray();
                DirectoryInfo f = new DirectoryInfo(Path.Combine(path_combination));
                CopyFilesRecursively(f.ToString(), outputPath);
                #endregion
            }
            #endregion

            #region Clothing
            if (doClothing || downloadAll) { 
                XmlDocument xmlDoc = new XmlDocument();
                string xmlData = request(urlFigureMap);
                xmlDoc.LoadXml(xmlData);
                XmlNode root = xmlDoc.DocumentElement;
                foreach (XmlNode node in root.ChildNodes)
                {
                    string name = node.Attributes["id"].Value.ToString();
                    AddToQueue(new Uri(ParseFormat($"{urlGordon}/{buildVersion}/{name}.swf")), $"/gordon/{buildVersion}/figure/");
                }
            
                xmlData = request(urlFigureMapV2);
                xmlDoc.LoadXml(xmlData);
                root = xmlDoc.DocumentElement;
                foreach (XmlNode node in root.ChildNodes)
                {
                    string name = node.Attributes["id"].Value.ToString();
                    AddToQueue(new Uri(ParseFormat($"{urlGordon}/{buildVersion}/{name}.swf")), $"/gordon/{buildVersion}/figure/");
                }
            }
            #endregion

            #region Effects
            if (doEffects || downloadAll) { 
                XmlDocument xmlDoc = new XmlDocument();
                string xmlData = request(urlEffectMap);
                xmlDoc.LoadXml(xmlData);
                XmlNode root = xmlDoc.DocumentElement;
                foreach (XmlNode node in root.ChildNodes)
                {
                    string name = node.Attributes["lib"].Value.ToString();
                    AddToQueue(new Uri(ParseFormat($"{urlGordon}/{buildVersion}/{name}.swf")), $"/gordon/{buildVersion}/effects/");
                }
            }
            #endregion

            #region Articles
            if (doArticles || downloadAll) {
                int canFail = 3;
                int totalFailed = 0;
                int matched = 0;
                string articlePattern = @"src="".*habbo-web-articles\/([^""]+\.png)"".+(?=class=""news-header__image news-header__image--thumbnail"">)";
                while (totalFailed <= canFail)
                {
                    Uri target = new Uri($"https://images.habbo.com/habbo-web-news/en/production/all_{matched}.html");

                    HttpWebRequest _request = (HttpWebRequest)WebRequest.Create(target);
                    _request.Method = "HEAD";
                    try
                    {
                        using (HttpWebResponse response = (HttpWebResponse)_request.GetResponse())
                        {
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                string source = request(target.ToString());
                                foreach (string Line in source.Split(Environment.NewLine.ToCharArray()))
                                {
                                    Match match = Regex.Match(Line, articlePattern);
                                    if (match.Success)
                                    {
                                        Uri uri = new Uri(ParseFormat(match.ToString()));
                                        string filename = uri.ToString().Split("/").Reverse().First();
                                        AddToQueue(uri, "/c_images/habbo-web-articles/");
                                        AddToQueue(new Uri($"`https://images.habbo.com/web_images/habbo-web-articles/{filename}"), "/c_images/habbo-web-articles/");
                                    }
                                }
                                totalFailed = 0;
                            }
                            else
                            {
                                totalFailed++;
                            }
                        }
                    }
                    catch
                    {
                        totalFailed++;
                    }
                    matched++;
                }
            }
            #endregion
            if (doGordon || downloadAll) { 
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/config_habbo.xml"), "");
                AddToQueue(new Uri(urlAvatarActions), "");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/HabboRoomContent.swf"), "");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/PlaceHolderFurniture.swf"), "");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/PlaceHolderPet.swf"), "");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/PlaceHolderWallItem.swf"), "");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/SelectionArrow.swf"), "");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/TileCursor.swf"), "");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/Habbo.swf"), "");
            }
            if (doGamedata || downloadAll) {
                String folder = Path.Combine(outputPath, "gamedata");
                if (!Directory.Exists(folder))
                {
                    // Try to create the directory.
                    Directory.CreateDirectory(folder);
                }

                string txt;
                txt = request(urlExternaltext);
                using(var sw = new StreamWriter(Path.Combine(outputPath, "gamedata", "external_flash_texts.txt"), true)){ sw.Write(txt); }

                txt = request(urlExternalvars);
                using (var sw = new StreamWriter(Path.Combine(outputPath, "gamedata", "external_variables.txt"), true)) { sw.Write(txt); }

                txt = request(urlProductdata);
                using(var sw = new StreamWriter(Path.Combine(outputPath, "gamedata", "productdata.txt"), true)) { sw.Write(txt); }

                txt = request(urlFurnidataTXT);
                using(var sw = new StreamWriter(Path.Combine(outputPath, "gamedata", "furnidata.txt"), true)) { sw.Write(txt); }

                txt = request("https://www.habbo.com/gamedata/override/external_flash_override_texts/0");
                using(var sw = new StreamWriter(Path.Combine(outputPath, "gamedata", "external_flash_override_texts.txt"), true)) { sw.Write(txt); }

                txt = request("https://www.habbo.com/gamedata/override/external_override_variables/0");
                using(var sw = new StreamWriter(Path.Combine(outputPath, "gamedata", "external_override_variables.txt"), true)) { sw.Write(txt); }

                txt = request("https://www.habbo.com/gamedata/productdata_json/0");
                using(var sw = new StreamWriter(Path.Combine(outputPath, "gamedata", "productdata.json"), true)) { sw.Write(txt); }

                txt = request("https://www.habbo.com/gamedata/furnidata_json/0");
                using(var sw = new StreamWriter(Path.Combine(outputPath, "gamedata", "furnidata.json"), true)) { sw.Write(txt); }

                XmlDocument xdoc;
                string xmlString;
                xdoc = new XmlDocument();
                xmlString = request(urlFurnidataXML);
                xdoc.LoadXml(xmlString);
                xdoc.Save(Path.Combine(outputPath, "gamedata", "furnidata.xml"));

                xdoc = new XmlDocument();
                xmlString = request(urlEffectMap);
                xdoc.LoadXml(xmlString);
                xdoc.Save(Path.Combine(outputPath, "gamedata", "effectmap.xml"));

                xdoc = new XmlDocument();
                xmlString = request(urlFigureMap);
                xdoc.LoadXml(xmlString);
                xdoc.Save(Path.Combine(outputPath, "gamedata", "figuremap.xml"));

                xdoc = new XmlDocument();
                xmlString = request(urlFigureMapV2);
                xdoc.LoadXml(xmlString);
                xdoc.Save(Path.Combine(outputPath, "gamedata", "figuremapV2.xml"));

                xdoc = new XmlDocument();
                xmlString = request("https://www.habbo.com/gamedata/figuredata/0");
                xdoc.LoadXml(xmlString);
                xdoc.Save(Path.Combine(outputPath, "gamedata", "figuredata.xml"));

                xdoc = new XmlDocument();
                xmlString = request("https://www.habbo.com/gamedata/productdata_xml/0");
                xdoc.LoadXml(xmlString);
                xdoc.Save(Path.Combine(outputPath, "gamedata", "productdata.xml"));
            }
            if (doParts || downloadAll) { 
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_basic_1.png"), "/c_images/Badgeparts/badgepart_base_basic_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_basic_2.png"), "/c_images/Badgeparts/badgepart_base_basic_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_basic_3.png"), "/c_images/Badgeparts/badgepart_base_basic_3.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_basic_4.png"), "/c_images/Badgeparts/badgepart_base_basic_4.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_basic_5.png"), "/c_images/Badgeparts/badgepart_base_basic_5.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_advanced_1.png"), "/c_images/Badgeparts/badgepart_base_advanced_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_advanced_2.png"), "/c_images/Badgeparts/badgepart_base_advanced_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_advanced_3.png"), "/c_images/Badgeparts/badgepart_base_advanced_3.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_advanced_4.png"), "/c_images/Badgeparts/badgepart_base_advanced_4.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_gold_1_part2.png"), "/c_images/Badgeparts/badgepart_base_gold_1_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_gold_1_part1.png"), "/c_images/Badgeparts/badgepart_base_gold_1_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_gold_2_part2.png"), "/c_images/Badgeparts/badgepart_base_gold_2_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_gold_2_part1.png"), "/c_images/Badgeparts/badgepart_base_gold_2_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_pin_part2.png"), "/c_images/Badgeparts/badgepart_base_pin_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_pin_part1.png"), "/c_images/Badgeparts/badgepart_base_pin_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_gradient_1.png"), "/c_images/Badgeparts/badgepart_base_gradient_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_gradient_2.png"), "/c_images/Badgeparts/badgepart_base_gradient_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_circles_1.png"), "/c_images/Badgeparts/badgepart_base_circles_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_circles_2.png"), "/c_images/Badgeparts/badgepart_base_circles_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_ornament_1_part2.png"), "/c_images/Badgeparts/badgepart_base_ornament_1_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_ornament_1_part1.png"), "/c_images/Badgeparts/badgepart_base_ornament_1_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_ornament_2_part2.png"), "/c_images/Badgeparts/badgepart_base_ornament_2_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_ornament_2_part1.png"), "/c_images/Badgeparts/badgepart_base_ornament_2_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_misc_1_part2.png"), "/c_images/Badgeparts/badgepart_base_misc_1_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_misc_1_part1.png"), "/c_images/Badgeparts/badgepart_base_misc_1_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_misc_2.png"), "/c_images/Badgeparts/badgepart_base_misc_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_beams_part2.png"), "/c_images/Badgeparts/badgepart_base_beams_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_beams_part1.png"), "/c_images/Badgeparts/badgepart_base_beams_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_ring.png"), "/c_images/Badgeparts/badgepart_base_ring.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_simplestar_part2.png"), "/c_images/Badgeparts/badgepart_base_simplestar_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_simplestar_part1.png"), "/c_images/Badgeparts/badgepart_base_simplestar_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_spiral.png"), "/c_images/Badgeparts/badgepart_base_spiral.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_book.png"), "/c_images/Badgeparts/badgepart_base_book.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_egg.png"), "/c_images/Badgeparts/badgepart_base_egg.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_ornament.png"), "/c_images/Badgeparts/badgepart_base_ornament.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_shield_part2.png"), "/c_images/Badgeparts/badgepart_base_shield_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_base_shield_part1.png"), "/c_images/Badgeparts/badgepart_base_shield_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_background_1.png"), "/c_images/Badgeparts/badgepart_symbol_background_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_background_2.png"), "/c_images/Badgeparts/badgepart_symbol_background_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_background_3_part2.png"), "/c_images/Badgeparts/badgepart_symbol_background_3_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_background_3_part1.png"), "/c_images/Badgeparts/badgepart_symbol_background_3_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_ball_1_part2.png"), "/c_images/Badgeparts/badgepart_symbol_ball_1_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_ball_1_part1.png"), "/c_images/Badgeparts/badgepart_symbol_ball_1_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_ball_2_part2.png"), "/c_images/Badgeparts/badgepart_symbol_ball_2_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_ball_2_part1.png"), "/c_images/Badgeparts/badgepart_symbol_ball_2_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bobba.png"), "/c_images/Badgeparts/badgepart_symbol_bobba.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bomb_part2.png"), "/c_images/Badgeparts/badgepart_symbol_bomb_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bomb_part1.png"), "/c_images/Badgeparts/badgepart_symbol_bomb_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bow.png"), "/c_images/Badgeparts/badgepart_symbol_bow.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_box_1.png"), "/c_images/Badgeparts/badgepart_symbol_box_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_box_2.png"), "/c_images/Badgeparts/badgepart_symbol_box_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bunting_1.png"), "/c_images/Badgeparts/badgepart_symbol_bunting_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bunting_2.png"), "/c_images/Badgeparts/badgepart_symbol_bunting_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_butterfly_part2.png"), "/c_images/Badgeparts/badgepart_symbol_butterfly_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_butterfly_part1.png"), "/c_images/Badgeparts/badgepart_symbol_butterfly_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_cowskull_part2.png"), "/c_images/Badgeparts/badgepart_symbol_cowskull_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_cowskull_part1.png"), "/c_images/Badgeparts/badgepart_symbol_cowskull_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_cross.png"), "/c_images/Badgeparts/badgepart_symbol_cross.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_diamond.png"), "/c_images/Badgeparts/badgepart_symbol_diamond.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_diploma_part2.png"), "/c_images/Badgeparts/badgepart_symbol_diploma_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_diploma_part1.png"), "/c_images/Badgeparts/badgepart_symbol_diploma_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_eyeball_part2.png"), "/c_images/Badgeparts/badgepart_symbol_eyeball_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_eyeball_part1.png"), "/c_images/Badgeparts/badgepart_symbol_eyeball_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_fist.png"), "/c_images/Badgeparts/badgepart_symbol_fist.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flame_1.png"), "/c_images/Badgeparts/badgepart_symbol_flame_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flame_2.png"), "/c_images/Badgeparts/badgepart_symbol_flame_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flash.png"), "/c_images/Badgeparts/badgepart_symbol_flash.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flower_1_part2.png"), "/c_images/Badgeparts/badgepart_symbol_flower_1_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flower_1_part1.png"), "/c_images/Badgeparts/badgepart_symbol_flower_1_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flower_2.png"), "/c_images/Badgeparts/badgepart_symbol_flower_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flower_3.png"), "/c_images/Badgeparts/badgepart_symbol_flower_3.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flower_4.png"), "/c_images/Badgeparts/badgepart_symbol_flower_4.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_football.png"), "/c_images/Badgeparts/badgepart_symbol_football.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_heart_1_part2.png"), "/c_images/Badgeparts/badgepart_symbol_heart_1_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_heart_1_part1.png"), "/c_images/Badgeparts/badgepart_symbol_heart_1_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_heart_2_part2.png"), "/c_images/Badgeparts/badgepart_symbol_heart_2_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_heart_2_part1.png"), "/c_images/Badgeparts/badgepart_symbol_heart_2_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_jingjang_part2.png"), "/c_images/Badgeparts/badgepart_symbol_jingjang_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_jingjang_part1.png"), "/c_images/Badgeparts/badgepart_symbol_jingjang_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_lips_part2.png"), "/c_images/Badgeparts/badgepart_symbol_lips_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_lips_part1.png"), "/c_images/Badgeparts/badgepart_symbol_lips_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_note.png"), "/c_images/Badgeparts/badgepart_symbol_note.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_peace.png"), "/c_images/Badgeparts/badgepart_symbol_peace.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_planet_part2.png"), "/c_images/Badgeparts/badgepart_symbol_planet_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_planet_part1.png"), "/c_images/Badgeparts/badgepart_symbol_planet_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_rainbow_part2.png"), "/c_images/Badgeparts/badgepart_symbol_rainbow_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_rainbow_part1.png"), "/c_images/Badgeparts/badgepart_symbol_rainbow_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_rosete.png"), "/c_images/Badgeparts/badgepart_symbol_rosete.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_shape.png"), "/c_images/Badgeparts/badgepart_symbol_shape.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_star_1.png"), "/c_images/Badgeparts/badgepart_symbol_star_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_star_2.png"), "/c_images/Badgeparts/badgepart_symbol_star_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_sword_1_part2.png"), "/c_images/Badgeparts/badgepart_symbol_sword_1_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_sword_1_part1.png"), "/c_images/Badgeparts/badgepart_symbol_sword_1_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_sword_2_part2.png"), "/c_images/Badgeparts/badgepart_symbol_sword_2_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_sword_2_part1.png"), "/c_images/Badgeparts/badgepart_symbol_sword_2_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_sword_3_part2.png"), "/c_images/Badgeparts/badgepart_symbol_sword_3_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_sword_3_part1.png"), "/c_images/Badgeparts/badgepart_symbol_sword_3_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_wings_1.png"), "/c_images/Badgeparts/badgepart_symbol_wings_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_wings_2.png"), "/c_images/Badgeparts/badgepart_symbol_wings_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_arrow_down.png"), "/c_images/Badgeparts/badgepart_symbol_arrow_down.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_arrow_left.png"), "/c_images/Badgeparts/badgepart_symbol_arrow_left.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_arrow_right.png"), "/c_images/Badgeparts/badgepart_symbol_arrow_right.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_arrow_up.png"), "/c_images/Badgeparts/badgepart_symbol_arrow_up.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_arrowbig_up.png"), "/c_images/Badgeparts/badgepart_symbol_arrowbig_up.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_axe_part2.png"), "/c_images/Badgeparts/badgepart_symbol_axe_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_axe_part1.png"), "/c_images/Badgeparts/badgepart_symbol_axe_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bug_part2.png"), "/c_images/Badgeparts/badgepart_symbol_bug_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bug_part1.png"), "/c_images/Badgeparts/badgepart_symbol_bug_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_capsbig_part2.png"), "/c_images/Badgeparts/badgepart_symbol_capsbig_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_capsbig_part1.png"), "/c_images/Badgeparts/badgepart_symbol_capsbig_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_capssmall_part2.png"), "/c_images/Badgeparts/badgepart_symbol_capssmall_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_capssmall_part1.png"), "/c_images/Badgeparts/badgepart_symbol_capssmall_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_cloud.png"), "/c_images/Badgeparts/badgepart_symbol_cloud.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_crown_part2.png"), "/c_images/Badgeparts/badgepart_symbol_crown_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_crown_part1.png"), "/c_images/Badgeparts/badgepart_symbol_crown_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_diamsmall2.png"), "/c_images/Badgeparts/badgepart_symbol_diamsmall2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_diamsmall.png"), "/c_images/Badgeparts/badgepart_symbol_diamsmall.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_drop.png"), "/c_images/Badgeparts/badgepart_symbol_drop.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_fingersheavy.png"), "/c_images/Badgeparts/badgepart_symbol_fingersheavy.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_fingersv.png"), "/c_images/Badgeparts/badgepart_symbol_fingersv.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_gtr_part2.png"), "/c_images/Badgeparts/badgepart_symbol_gtr_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_gtr_part1.png"), "/c_images/Badgeparts/badgepart_symbol_gtr_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_hat.png"), "/c_images/Badgeparts/badgepart_symbol_hat.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_oval_part2.png"), "/c_images/Badgeparts/badgepart_symbol_oval_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_oval_part1.png"), "/c_images/Badgeparts/badgepart_symbol_oval_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_pawprint.png"), "/c_images/Badgeparts/badgepart_symbol_pawprint.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_screw.png"), "/c_images/Badgeparts/badgepart_symbol_screw.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_stickL_part2.png"), "/c_images/Badgeparts/badgepart_symbol_stickL_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_stickL_part1.png"), "/c_images/Badgeparts/badgepart_symbol_stickL_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_stickR_part2.png"), "/c_images/Badgeparts/badgepart_symbol_stickR_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_stickR_part1.png"), "/c_images/Badgeparts/badgepart_symbol_stickR_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_alligator.png"), "/c_images/Badgeparts/badgepart_symbol_alligator.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_americanfootball_part2.png"), "/c_images/Badgeparts/badgepart_symbol_americanfootball_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_americanfootball_part1.png"), "/c_images/Badgeparts/badgepart_symbol_americanfootball_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_award_part2.png"), "/c_images/Badgeparts/badgepart_symbol_award_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_award_part1.png"), "/c_images/Badgeparts/badgepart_symbol_award_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bananapeel.png"), "/c_images/Badgeparts/badgepart_symbol_bananapeel.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_battleball.png"), "/c_images/Badgeparts/badgepart_symbol_battleball.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_biohazard.png"), "/c_images/Badgeparts/badgepart_symbol_biohazard.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bird.png"), "/c_images/Badgeparts/badgepart_symbol_bird.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_bishop.png"), "/c_images/Badgeparts/badgepart_symbol_bishop.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_coalion.png"), "/c_images/Badgeparts/badgepart_symbol_coalion.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_cocoamug.png"), "/c_images/Badgeparts/badgepart_symbol_cocoamug.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_dashflag.png"), "/c_images/Badgeparts/badgepart_symbol_dashflag.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_diamondring_part2.png"), "/c_images/Badgeparts/badgepart_symbol_diamondring_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_diamondring_part1.png"), "/c_images/Badgeparts/badgepart_symbol_diamondring_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_discoball_part2.png"), "/c_images/Badgeparts/badgepart_symbol_discoball_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_discoball_part1.png"), "/c_images/Badgeparts/badgepart_symbol_discoball_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_dog.png"), "/c_images/Badgeparts/badgepart_symbol_dog.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_electricguitarh_part2.png"), "/c_images/Badgeparts/badgepart_symbol_electricguitarh_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_electricguitarh_part1.png"), "/c_images/Badgeparts/badgepart_symbol_electricguitarh_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_electricguitarv_part2.png"), "/c_images/Badgeparts/badgepart_symbol_electricguitarv_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_electricguitarv_part1.png"), "/c_images/Badgeparts/badgepart_symbol_electricguitarv_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_film.png"), "/c_images/Badgeparts/badgepart_symbol_film.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flame_part2.png"), "/c_images/Badgeparts/badgepart_symbol_flame_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_flame_part1.png"), "/c_images/Badgeparts/badgepart_symbol_flame_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_gamepad.png"), "/c_images/Badgeparts/badgepart_symbol_gamepad.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_gem1_part2.png"), "/c_images/Badgeparts/badgepart_symbol_gem1_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_gem1_part1.png"), "/c_images/Badgeparts/badgepart_symbol_gem1_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_gem2_part2.png"), "/c_images/Badgeparts/badgepart_symbol_gem2_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_gem2_part1.png"), "/c_images/Badgeparts/badgepart_symbol_gem2_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_gem3_part2.png"), "/c_images/Badgeparts/badgepart_symbol_gem3_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_gem3_part1.png"), "/c_images/Badgeparts/badgepart_symbol_gem3_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_hawk.png"), "/c_images/Badgeparts/badgepart_symbol_hawk.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_hearts_down.png"), "/c_images/Badgeparts/badgepart_symbol_hearts_down.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_hearts_up.png"), "/c_images/Badgeparts/badgepart_symbol_hearts_up.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_horseshoe.png"), "/c_images/Badgeparts/badgepart_symbol_horseshoe.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_inksplatter.png"), "/c_images/Badgeparts/badgepart_symbol_inksplatter.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_leaf.png"), "/c_images/Badgeparts/badgepart_symbol_leaf.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_micstand.png"), "/c_images/Badgeparts/badgepart_symbol_micstand.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_mirror_part2.png"), "/c_images/Badgeparts/badgepart_symbol_mirror_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_mirror_part1.png"), "/c_images/Badgeparts/badgepart_symbol_mirror_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_monkeywrench.png"), "/c_images/Badgeparts/badgepart_symbol_monkeywrench.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_note1.png"), "/c_images/Badgeparts/badgepart_symbol_note1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_note2.png"), "/c_images/Badgeparts/badgepart_symbol_note2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_note3.png"), "/c_images/Badgeparts/badgepart_symbol_note3.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_nursecross.png"), "/c_images/Badgeparts/badgepart_symbol_nursecross.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_pencil_part2.png"), "/c_images/Badgeparts/badgepart_symbol_pencil_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_pencil_part1.png"), "/c_images/Badgeparts/badgepart_symbol_pencil_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_queen.png"), "/c_images/Badgeparts/badgepart_symbol_queen.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_rock.png"), "/c_images/Badgeparts/badgepart_symbol_rock.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_rook.png"), "/c_images/Badgeparts/badgepart_symbol_rook.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_skate.png"), "/c_images/Badgeparts/badgepart_symbol_skate.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_smallring_part2.png"), "/c_images/Badgeparts/badgepart_symbol_smallring_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_smallring_part1.png"), "/c_images/Badgeparts/badgepart_symbol_smallring_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_snowstorm_part2.png"), "/c_images/Badgeparts/badgepart_symbol_snowstorm_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_snowstorm_part1.png"), "/c_images/Badgeparts/badgepart_symbol_snowstorm_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_sphere.png"), "/c_images/Badgeparts/badgepart_symbol_sphere.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_spraycan_part2.png"), "/c_images/Badgeparts/badgepart_symbol_spraycan_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_spraycan_part1.png"), "/c_images/Badgeparts/badgepart_symbol_spraycan_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_stars1.png"), "/c_images/Badgeparts/badgepart_symbol_stars1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_stars2.png"), "/c_images/Badgeparts/badgepart_symbol_stars2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_stars3.png"), "/c_images/Badgeparts/badgepart_symbol_stars3.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_stars4.png"), "/c_images/Badgeparts/badgepart_symbol_stars4.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_stars5.png"), "/c_images/Badgeparts/badgepart_symbol_stars5.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_waterdrop_part2.png"), "/c_images/Badgeparts/badgepart_symbol_waterdrop_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_waterdrop_part1.png"), "/c_images/Badgeparts/badgepart_symbol_waterdrop_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_wolverine.png"), "/c_images/Badgeparts/badgepart_symbol_wolverine.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_0.png"), "/c_images/Badgeparts/badgepart_symbol_0.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_1.png"), "/c_images/Badgeparts/badgepart_symbol_1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_2.png"), "/c_images/Badgeparts/badgepart_symbol_2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_3.png"), "/c_images/Badgeparts/badgepart_symbol_3.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_4.png"), "/c_images/Badgeparts/badgepart_symbol_4.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_5.png"), "/c_images/Badgeparts/badgepart_symbol_5.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_6.png"), "/c_images/Badgeparts/badgepart_symbol_6.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_7.png"), "/c_images/Badgeparts/badgepart_symbol_7.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_8.png"), "/c_images/Badgeparts/badgepart_symbol_8.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_9.png"), "/c_images/Badgeparts/badgepart_symbol_9.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_a.png"), "/c_images/Badgeparts/badgepart_symbol_a.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_b.png"), "/c_images/Badgeparts/badgepart_symbol_b.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_c.png"), "/c_images/Badgeparts/badgepart_symbol_c.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_d.png"), "/c_images/Badgeparts/badgepart_symbol_d.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_e.png"), "/c_images/Badgeparts/badgepart_symbol_e.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_f.png"), "/c_images/Badgeparts/badgepart_symbol_f.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_g.png"), "/c_images/Badgeparts/badgepart_symbol_g.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_h.png"), "/c_images/Badgeparts/badgepart_symbol_h.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_i.png"), "/c_images/Badgeparts/badgepart_symbol_i.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_j.png"), "/c_images/Badgeparts/badgepart_symbol_j.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_k.png"), "/c_images/Badgeparts/badgepart_symbol_k.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_l.png"), "/c_images/Badgeparts/badgepart_symbol_l.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_m.png"), "/c_images/Badgeparts/badgepart_symbol_m.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_n.png"), "/c_images/Badgeparts/badgepart_symbol_n.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_o.png"), "/c_images/Badgeparts/badgepart_symbol_o.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_p.png"), "/c_images/Badgeparts/badgepart_symbol_p.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_q.png"), "/c_images/Badgeparts/badgepart_symbol_q.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_r.png"), "/c_images/Badgeparts/badgepart_symbol_r.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_s.png"), "/c_images/Badgeparts/badgepart_symbol_s.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_t.png"), "/c_images/Badgeparts/badgepart_symbol_t.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_u.png"), "/c_images/Badgeparts/badgepart_symbol_u.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_v.png"), "/c_images/Badgeparts/badgepart_symbol_v.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_w.png"), "/c_images/Badgeparts/badgepart_symbol_w.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_x.png"), "/c_images/Badgeparts/badgepart_symbol_x.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_y.png"), "/c_images/Badgeparts/badgepart_symbol_y.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_z.png"), "/c_images/Badgeparts/badgepart_symbol_z.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_pixel_part2.png"), "/c_images/Badgeparts/badgepart_symbol_pixel_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_pixel_part1.png"), "/c_images/Badgeparts/badgepart_symbol_pixel_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_credit_part2.png"), "/c_images/Badgeparts/badgepart_symbol_credit_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_credit_part1.png"), "/c_images/Badgeparts/badgepart_symbol_credit_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_hc_part2.png"), "/c_images/Badgeparts/badgepart_symbol_hc_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_hc_part1.png"), "/c_images/Badgeparts/badgepart_symbol_hc_part1.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_vip_part2.png"), "/c_images/Badgeparts/badgepart_symbol_vip_part2.png");
                AddToQueue(new Uri("https://images.habbo.com/c_images/Badgeparts/badgepart_symbol_vip_part1.png"), "/c_images/Badgeparts/badgepart_symbol_vip_part1.png");
            }

            if (doPets || downloadAll) {
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/bear.swf"), $"/gordon/{buildVersion}/pets/bear.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/bearbaby.swf"), $"/gordon/{buildVersion}/pets/bearbaby.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/bunnydepressed.swf"), $"/gordon/{buildVersion}/pets/bunnydepressed.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/bunnyeaster.swf"), $"/gordon/{buildVersion}/pets/bunnyeaster.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/bunnyevil.swf"), $"/gordon/{buildVersion}/pets/bunnyevil.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/bunnylove.swf"), $"/gordon/{buildVersion}/pets/bunnylove.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/cat.swf"), $"/gordon/{buildVersion}/pets/cat.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/chicken.swf"), $"/gordon/{buildVersion}/pets/chicken.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/cow.swf"), $"/gordon/{buildVersion}/pets/cow.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/croco.swf"), $"/gordon/{buildVersion}/pets/croco.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/demonmonkey.swf"), $"/gordon/{buildVersion}/pets/demonmonkey.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/dog.swf"), $"/gordon/{buildVersion}/pets/dog.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/dragon.swf"), $"/gordon/{buildVersion}/pets/dragon.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/fools.swf"), $"/gordon/{buildVersion}/pets/fools.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/frog.swf"), $"/gordon/{buildVersion}/pets/frog.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/gnome.swf"), $"/gordon/{buildVersion}/pets/gnome.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/haloompa.swf"), $"/gordon/{buildVersion}/pets/haloompa.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/horse.swf"), $"/gordon/{buildVersion}/pets/horse.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/kittenbaby.swf"), $"/gordon/{buildVersion}/pets/kittenbaby.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/lion.swf"), $"/gordon/{buildVersion}/pets/lion.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/monkey.swf"), $"/gordon/{buildVersion}/pets/monkey.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/monster.swf"), $"/gordon/{buildVersion}/pets/monster.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/monsterplant.swf"), $"/gordon/{buildVersion}/pets/monsterplant.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/pig.swf"), $"/gordon/{buildVersion}/pets/pig.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/pigeonevil.swf"), $"/gordon/{buildVersion}/pets/pigeonevil.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/pigeongood.swf"), $"/gordon/{buildVersion}/pets/pigeongood.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/pigletbaby.swf"), $"/gordon/{buildVersion}/pets/pigletbaby.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/pterosaur.swf"), $"/gordon/{buildVersion}/pets/pterosaur.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/puppybaby.swf"), $"/gordon/{buildVersion}/pets/puppybaby.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/rhino.swf"), $"/gordon/{buildVersion}/pets/rhino.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/spider.swf"), $"/gordon/{buildVersion}/pets/spider.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/terrier.swf"), $"/gordon/{buildVersion}/pets/terrier.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/terrierbaby.swf"), $"/gordon/{buildVersion}/pets/terrierbaby.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/turtle.swf"), $"/gordon/{buildVersion}/pets/turtle.swf");
                AddToQueue(new Uri($"{urlGordon}/{buildVersion}/velociraptor.swf"), $"/gordon/{buildVersion}/pets/velociraptor.swf");
            }

            if (doArchive || downloadAll) { 
                ScrapeLink(new Uri("https://www.habborator.org/archive/boxes.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/catalogue.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/items.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/characters.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/frank.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/callie.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/webview.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/nav_1.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/nav_2.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/nav_3.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/nav_1.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/nav_2.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/nav_3.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/banners.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/banners2.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/partner.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/hotcampaigns.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/interstitials.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/topstory_generic.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/topstory_1.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/topstory_2.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/topstory_ae.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/topstory_fm.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/topstory_ns.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/topstory_tz.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/topstory_promo.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/habblets.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/stickers.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/alphabet.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/xmas.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/love.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/habbowood.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/occasional.php"));
                ScrapeLink(new Uri("https://www.habborator.org/archive/limited.php"));
            }

            string[] discoveries = { urlExternaltext, urlExternalvars, urlProductdata, urlFurnidataTXT, urlFurnidataXML, urlFigureData, urlEffectMap, urlFigureMap, urlFigureMapV2, urlAvatarActions };
            foreach (string discovery in discoveries)
            {
                if (debug) { Console.WriteLine($"|[---]|  WORKING-ON: {discovery}"); }
                ScrapeLink(new Uri(discovery));
            };

            isRunning = false;
        }

        #region Production build selector
        private void latestBuildVersion()
        {
            string source = request(urlExternalvars);
            foreach (string Line in source.Split(Environment.NewLine.ToCharArray()))
            {
                if (!Line.Contains("flash.client.url="))
                {
                    continue;
                }
                buildVersion = Line.Substring(0, Line.Length - 1).Split('/')[4];
                Console.WriteLine("Current habbo release: " + buildVersion);
                Console.WriteLine("*  Working ...");
            }
        }
        #endregion
    }
}
#endregion