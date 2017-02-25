using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RssFeedGenerator
{
    class Program
    {
        static int Main( string[] args )
        {
            try
            {
                if( ( args.Length != 1 ) && ( args.Length != 2 ) )
                {
                    PrintUsage();
                    return 1;
                }
                else if( args[0] == "--help" )
                {
                    PrintUsage();
                    return 0;
                }

                RssGenerator generator = new RssGenerator( args[0] );
                generator.OpenProfiles();
                generator.DownloadFeeds();
                generator.GenerateGlobalRss( args[1] );
            }
            catch( Exception e )
            {
                Console.WriteLine( "UNHANDLED EXCEPTION!" );
                Console.WriteLine( e );
                return -1;
            }

            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine( "Usage: RssFeedGenerator.exe pathToProfiles globalRssOutputPath" );
        }
    }
}
