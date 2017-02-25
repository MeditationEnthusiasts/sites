using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
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
                string fileContents = File.ReadAllText( Path.Combine( profile, "index.md" ) );
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
                    using( WebClient client = new WebClient() )
                    {
                        string rssContents = client.DownloadString( profile.Value );
                        File.WriteAllText( Path.Combine( profile.Key, "feed.xml" ), rssContents );
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
                    using( XmlReader reader = XmlReader.Create( Path.Combine( profile.Key, "feed.xml" ) ) )
                    {
                        feed = SyndicationFeed.Load( reader );
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
