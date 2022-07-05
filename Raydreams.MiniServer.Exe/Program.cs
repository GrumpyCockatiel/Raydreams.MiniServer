using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Markdig;
using Newtonsoft.Json;

namespace Raydreams.MiniServer
{
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
            MoreServer server = new MoreServer( 50001, Path.Combine( ServerPath, ServerFolder ) )
            { ConvertMarkdown = new Markdigdowner().GetHtml };

            // explicit start of the server and block
            await server.Serve();

            return 0;
        }
    }

    /// <summary>Override the basic server and add your own custom route handlers</summary>
    public class MoreServer : MiniServer
    {
        public MoreServer( int port, string rootPath ) : base(port, rootPath)
        {
            // add more custom routes here
            this.SpecialRoutes.Add( "/json", ServeJSONExample );
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

        /// <summary>Customize how to log</summary>
        /// <param name="msg"></param>
        protected override void LogIt( string msg )
        {
            base.LogIt( msg );

            Console.WriteLine( msg );
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
}
