string target = Argument( "target", "taste" );

const string pretzelExe = "./_pretzel/src/Pretzel/bin/Debug/netcoreapp3.1/Pretzel.dll";
const string rssFeedExe = "./_rssfeed/RssFeedGenerator/bin/Debug/netcoreapp3.1/RssFeedGenerator.dll";
const string pluginDir = "./_plugins";
const string categoryPlugin = "./_plugins/Pretzel.Categories.dll";
const string extensionPlugin = "./_plugins/Pretzel.SethExtensions.dll";
const string rssDll = "./_rssfeed/RssFeedGenerator/bin/Debug/netcoreapp3.1/System.ServiceModel.Syndication.dll";
const string rssDllPlugin = "./_plugins/System.ServiceModel.Syndication.dll";
const string atomfeed = "globalatomfeed.xml";
const string rssfeed = "globalrssfeed.xml";

// ---------------- Tasks ----------------

Task( "taste" )
.Does(
    () =>
    {
        CheckRssFeedDependency();
        RunPretzel( "taste", false );
    }
).Description( "Calls pretzel taste to try the site locally" );

Task( "generate_rss_feeds" )
.Does(
    () =>
    {
        GenerateFeeds();
    }
).Description( "Generates the RSS feeds for all sites" );

Task( "generate" )
.Does(
    () =>
    {
        EnsureDirectoryExists( "_site" );
        CleanDirectory( "_site" );
        CheckRssFeedDependency();
        RunPretzel( "bake", true );
    }
).Description( "Builds the site for publishing." );

Task( "build_pretzel" )
.Does(
    () =>
    {
        BuildPretzel();
    }
).Description( "Compiles Pretzel" );

Task( "build_all" )
.IsDependentOn( "build_pretzel" )
.IsDependentOn( "generate_rss_feeds" )
.IsDependentOn( "taste" );

// ---------------- Functions  ----------------

void BuildPretzel()
{
    Information( "Building Pretzel..." );

    DotNetCoreBuildSettings settings = new DotNetCoreBuildSettings
    {
        Configuration = "Debug"
    };

    DotNetCoreBuild( "./Sites.sln", settings );

    EnsureDirectoryExists( pluginDir );

    // Move Pretzel.Categories.
    {
        FilePathCollection files = GetFiles( "./_pretzel/src/Pretzel.Categories/bin/Debug/netstandard2.1/Pretzel.Categories.*" );
        CopyFiles( files, Directory( pluginDir ) );
    }

    // Move Pretzel.SethExtensions
    {
        FilePathCollection files = GetFiles( "./_pretzel/src/Pretzel.SethExtensions/bin/Debug/netstandard2.1/Pretzel.SethExtensions.*" );
        CopyFiles( files, Directory( pluginDir ) );
    }

    // Move System.ServiceModel.Syndication
    {
        FilePath dll = File( rssDll );
        CopyFile( dll, rssDllPlugin );
    }

    Information( "Building Pretzel... Done!" );
}

void RunPretzel( string argument, bool abortOnFail )
{
    CheckPretzelDependency();

    bool fail = false;
    string onStdOut( string line )
    {
        if( string.IsNullOrWhiteSpace( line ) )
        {
            return line;
        }
        else if( line.StartsWith( "Failed to render template" ) )
        {
            fail = true;
        }

        Console.WriteLine( line );

        return line;
    }

    ProcessSettings settings = new ProcessSettings
    {
        Arguments = ProcessArgumentBuilder.FromString( $"\"{pretzelExe}\" {argument} --debug" ),
        Silent = false,
        RedirectStandardOutput = abortOnFail,
        RedirectedStandardOutputHandler = onStdOut
    };

    int exitCode = StartProcess( "dotnet", settings );
    if( exitCode != 0 )
    {
        throw new Exception( $"Pretzel exited with exit code: {exitCode}" );
    }

    if( abortOnFail && fail )
    {
        throw new Exception( "Failed to render template" );   
    }
}

void CheckPretzelDependency()
{
    if(
        ( FileExists( pretzelExe ) == false ) ||
        ( FileExists( categoryPlugin ) == false ) ||
        ( FileExists( extensionPlugin ) == false ) ||
        ( FileExists( rssFeedExe ) == false ) ||
        ( FileExists( rssDllPlugin ) == false )
    )
    {
        BuildPretzel();
    }
}

void GenerateFeeds()
{
    string argsStr = $"{rssFeedExe} _posts .";
    ProcessArgumentBuilder args = ProcessArgumentBuilder.FromString( argsStr );
    ProcessSettings settings = new ProcessSettings
    {
        Arguments = args
    };

    int exitCode = StartProcess( "dotnet", settings );
    if( exitCode != 0 )
    {
        throw new Exception(
            $"Feed generation exited with code: {exitCode}"
        );
    }
}

void CheckRssFeedDependency()
{
    CheckPretzelDependency();

    if( 
        ( FileExists( atomfeed ) == false ) ||
        ( FileExists( rssfeed ) == false )
    )
    {
        GenerateFeeds();
    }
}

RunTarget( target );
