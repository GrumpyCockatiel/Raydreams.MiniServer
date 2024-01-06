using System.Net;
using Markdig;
using Newtonsoft.Json;

namespace Raydreams.Web.CLI;

/// <summary>The mini server itself</summary>
public class Program
{
    /// <summary>This is the server local path</summary>
    /// <remarks>no trailing slash in the path</remarks>
    public static readonly string ServerPath = Environment.GetFolderPath( Environment.SpecialFolder.DesktopDirectory );

    /// <summary>server folder inside the path to serve content from</summary>
    public static readonly string ServerFolder = "www";

    /// <summary></summary>
    /// <param name="args"></param>
    static async Task<int> Main( string[] args )
    {
        //MoreServer server = new MoreServer( 50001, Path.Combine( ServerPath, ServerFolder ) )
        MoreServer server = new MoreServer( 50001, null )
        { ConvertMarkdown = new Markdigdowner().GetHtml };

        // you can override the log methods OR capture the events with you own logger
        server.OnLog += DoLog;

        // explicit start of the server and block
        await server.Serve();

        return 0;
    }

    /// <summary>Capture log events</summary>
    /// <param name="sender">sender</param>
    /// <param name="message">log message</param>
    /// <param name="level">standard log level</param>
    protected static void DoLog(object sender, string message, string level)
    {
        Console.WriteLine( message );
    }
}

/// <summary>Override the basic server and add your own custom route handlers</summary>
public class MoreServer : MiniServer
{
    public MoreServer( int port, string? rootPath ) : base(port, rootPath)
    {
        // add more custom routes here
        this.AddSpecialRoute( "/json", ServeJSONExample );
        this.AddSpecialRoute( "/token", ServeToken );
    }

    /// <summary>Example on how to add custom routes</summary>
    /// <param name="ctx"></param>
    protected void ServeJSONExample( HttpListenerContext ctx )
    {
        var jsonObj = new { Timestamp = DateTimeOffset.UtcNow, Message = "My Special Object" };

        // add a event callback to your own code here

        // echo the Proxy JSON response to the browser window - not really necessary to work
        base.ServeJSON( ctx.Response, JsonConvert.SerializeObject( jsonObj ) );
    }

    /// <summary>Example callback from OAuth with an access token</summary>
    /// <param name="ctx"></param>
    protected void ServeToken( HttpListenerContext ctx )
    {
        // get the access token from the code param
        string? code = ctx.Request.QueryString.Get( "code" );

        if ( String.IsNullOrWhiteSpace( code ) )
            code = "no code";

        // add a event callback to your own code here to set the access token in your app

        // send something to the browser window so they know it worked
        this.ServeSimpleHTML( ctx.Response, $"<p>Your Access Token Is</p><p>{code}</p>" );
    }
}

/// <summary>Uses Markdig package to convert Markdown to HTML</summary>
public class Markdigdowner
{
    public Markdigdowner()
    {
        // create the Markdown converter
        this.MarkdownEngine = new MarkdownPipelineBuilder().UseSoftlineBreakAsHardlineBreak().Build();
    }

    /// <summary>The Markdown Pipeline</summary>
    protected MarkdownPipeline MarkdownEngine { get; set; }

    public string GetHtml(string md)
    {
        if ( String.IsNullOrWhiteSpace( md ) )
            return String.Empty;

        return Markdown.ToHtml( md, this.MarkdownEngine );
    }
}
