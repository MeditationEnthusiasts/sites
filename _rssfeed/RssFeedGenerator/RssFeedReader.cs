
//          Copyright Seth Hendrick 2017.
// Distributed under the Boost Software License, Version 1.0.
//    (See accompanying file LICENSE_1_0.txt or copy at
//          http://www.boost.org/LICENSE_1_0.txt)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace RssFeedGenerator
{
    public class RssGenerator
    {
        // ---------------- Fields ----------------

        private readonly string pathToProfiles;

        private Dictionary<string, string> profilePathList;

        private static readonly Regex rssRegex = new Regex(
            @"---*\s+.+rss:\s*(?<rssUrl>[^\s]+)\s+.+---*",
            RegexOptions.Compiled | RegexOptions.Singleline
        );

        private const string userAgent = "Meditation Enthusiasts Sites RSS Reader";

        /// <summary>
        /// Max size is 1MB.  Anything bigger and we may have someone trying to
        /// fill our server.
        /// </summary>
        private const int maxFileSize = 1 * 1000 * 1000;

        // ---------------- Constructor ----------------

        public RssGenerator( string pathToProfiles )
        {
            this.pathToProfiles = pathToProfiles;
            this.profilePathList = new Dictionary<string, string>();
        }

        // ---------------- Properties ----------------

        // ---------------- Functions ----------------

        public void OpenProfiles()
        {
            Console.WriteLine( "Reading Files..." );

            string[] profilePaths = Directory.GetDirectories( this.pathToProfiles );
            foreach( string profile in profilePaths )
            {
                string profileName = Path.GetFileName( profile );

                string fileContents = File.ReadAllText( Path.Combine( profile, profileName + ".md" ) );
                Match match = rssRegex.Match( fileContents );
                if( match.Success )
                {
                    string url = match.Groups["rssUrl"].Value;
                    if( string.IsNullOrEmpty( url ) == false )
                    {
                        this.profilePathList[profile] = url;
                    }
                }
            }

            Console.WriteLine( "Reading Files...Done!" );
        }

        public void DownloadFeeds()
        {
            Console.WriteLine( "Downloading Feeds..." );

            foreach( KeyValuePair<string, string> profile in this.profilePathList )
            {
                try
                {
                    string profileName = Path.GetFileName( profile.Key );

                    using( WebClient client = new WebClient() )
                    {
                        client.Headers.Add( "user-agent", userAgent );

                        client.DownloadProgressChanged += delegate ( object sender, DownloadProgressChangedEventArgs e )
                        {
                            if( e.TotalBytesToReceive >= maxFileSize )
                            {
                                client.CancelAsync();
                                Console.WriteLine( "Web Request Cancelled due to size of {0}", e.TotalBytesToReceive );
                            }
                        };

                        Task<string> rssContents = client.DownloadStringTaskAsync( profile.Value );
                        rssContents.Wait();

                        StringBuilder builder = new StringBuilder();
                        builder.AppendLine( "---" );
                        builder.AppendLine( "permalink: /profile/" + profileName + "/feed.xml" );
                        builder.AppendLine( "---" );
                        builder.AppendLine( rssContents.Result );

                        File.WriteAllText( Path.Combine( profile.Key, "feed.xml" ), builder.ToString() );
                    }
                }
                catch( Exception e )
                {
                    Console.WriteLine( "Could not get feed of {0} for reason {1}{2}", profile.Key, Environment.NewLine, e.ToString() );
                }
            }

            Console.WriteLine( "Downloading Feeds...Done!" );
        }

        public void GenerateGlobalRss( string outputPath )
        {
            Console.WriteLine( "Generating Global Rss..." );

            List<SyndicationItem> items = new List<SyndicationItem>();

            foreach( KeyValuePair<string, string> profile in this.profilePathList )
            {
                try
                {
                    Console.WriteLine( "Reading feed for {0}", profile.Key );

                    SyndicationFeed feed = null;
                    string fileName = Path.Combine( profile.Key, "feed.xml" );
                    using( StringReader strReader = new StringReader( File.ReadAllText( fileName ) ) )
                    {
                        // Skip permalink information
                        strReader.ReadLine(); // ---
                        strReader.ReadLine(); // permalink: /profile/something/feed.xml
                        strReader.ReadLine(); // ---

                        using( XmlReader reader = XmlReader.Create( strReader ) )
                        {
                            feed = SyndicationFeed.Load( reader );
                        }
                    }

                    int count = 0;
                    foreach( SyndicationItem item in feed.Items )
                    {
                        // Only include 5 from each site, don't want one over crowding.
                        if( count >= 5 )
                        {
                            break;
                        }

                        if( string.IsNullOrEmpty( item.Title.Text ) || string.IsNullOrEmpty( item.Summary.Text ) )
                        {
                            continue;
                        }

                        items.Add( item );
                        ++count;
                    }
                }
                catch( Exception e )
                {
                    Console.WriteLine( "Could not get feed of {0} for reason {1}{2}", profile.Key, Environment.NewLine, e.ToString() );
                }
            }

            // Sort.
            items = items.OrderByDescending( i => i.PublishDate ).ToList();

            // Only have 100 items.
            if( items.Count >= 100 )
            {
                items = items.GetRange( 0, 99 );
            }

            // Save
            SyndicationFeed globalFeed = new SyndicationFeed( items );
            globalFeed.Title = new TextSyndicationContent( "Meditation Enthusiasts Sites Global RSS feed" );
            globalFeed.Description = new TextSyndicationContent(
                "All of the sites submitted to Meditation Enthusiasts Sites RSS feeds combined into one!"
            );
            globalFeed.ImageUrl = new Uri( "/img/logo.png", UriKind.Relative );
            using( XmlWriter writer = XmlWriter.Create( Path.Combine( outputPath, "globalatomfeed.xml" ) ) )
            {
                globalFeed.SaveAsAtom10( writer );
            }

            using( XmlWriter writer = XmlWriter.Create( Path.Combine( outputPath, "globalrssfeed.xml" ) ) )
            {
                globalFeed.SaveAsRss20( writer );
            }

            Console.WriteLine( "Generating Global Rss...Done!" );
        }
    }
}
