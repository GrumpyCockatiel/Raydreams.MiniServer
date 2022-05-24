using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Raydreams.MiniServer
{
    /// <summary>Delegate Function for converting Markdown to HTML</summary>
    /// <param name="md">The markdown to convert</param>
    /// <returns>Converted HTML</returns>
    public delegate string MarkdownToHTML( string md );

    /// <summary>Portable mini web server</summary>
    public class MiniServer
    {
        public const string HTMLType = "text/html; charset=utf-8";

        public const string PNGType = "image/png";

        public const string JPEGType = "image/jpg";

        public const string GIFType = "image/gif";

        public const string CSSType = "text/css";

        /// <summary>only valid extensions that can be served except .ico</summary>
        public static readonly string[] Extensions = new string[] { ".htm", ".html", ".css", ".md", ".markdown", ".png", ".gif", ".jpg", ".jpeg" };

        /// <summary>Default page to return</summary>
        public static readonly string HomePage = "index.html";

        private bool _shutdown = false;

        #region [ Constructors ]

        /// <summary>Constructor</summary>
        /// <param name="port">Port to run on</param>
        /// <param name="rootPath">Path to the root of the physical web folder to server files from</param>
        public MiniServer( int port, string wwwRoot )
        {
            if ( String.IsNullOrWhiteSpace( wwwRoot ) )
                throw new ArgumentException( "Root path required", nameof( wwwRoot ) );

            this.RootFolder = new DirectoryInfo( wwwRoot );

            // because some people still use port 3000
            this.Port = Math.Clamp(port, 1024, 65535 );

            // built in special routes
            this.SpecialRoutes = new Dictionary<string, RouteHandler>()
            {
                { "/sig", this.ServeSignature },
                { "/test", this.ServeTest },
                { "/favicon.ico", this.ServeFavicon }
            };
        }

        #endregion [ Constructors ]

        #region [ Properties ]

        /// <summary>Delegate method to handle custom routes</summary>
        public delegate void RouteHandler( HttpListenerContext context );

        /// <summary>The physical root web server file path</summary>
        public DirectoryInfo RootFolder { get; set; }

        /// <summary>When set to false, no physical pages can ever be served - only special paths</summary>
        public bool EnablePageServe { get; set; } = true;

        /// <summary>port to listen on</summary>
        public int Port { get; set; } = 50005;

        /// <summary>Collection of custom routes</summary>
        protected Dictionary<string, RouteHandler> SpecialRoutes;

        /// <summary>Is the server still running</summary>
        public bool IsRunning => !this._shutdown;

        /// <summary>The Markdown Pipeline</summary>
        public MarkdownToHTML ConvertMarkdown { get; set; } = ( string md ) => "<h1>Markdown not supported!</h1>";

        #endregion [ Properties ]

        #region [ Methods ]

        /// <summary>Explicit call to shutdown</summary>
        public void Shutdown()
        {
            this._shutdown = true;
        }

        /// <summary>Start serving files</summary>
        public async Task<int> Serve()
        {
            // check to see if we can start the server
            if ( !HttpListener.IsSupported )
                return -1;

            // setup server and listen on the following ports
            HttpListener server = new HttpListener();
            server.Prefixes.Add( $"http://localhost:{Port}/" );
            server.Prefixes.Add( $"http://{IPAddress.Loopback}:{Port}/" );
            
            // start listening
            server.Start();

            this.LogIt( $"Dr. Frasier Crane here... I'm Listening at {server.Prefixes.First()}" );

            // endless loop until force quit for now
            while ( this.IsRunning )
            {
                // block for a new request
                HttpListenerContext context = await server.GetContextAsync();

                try
                {
                    // always check shutdown first - nothing can override it
                    if ( context.Request.Url.LocalPath == "/shutdown" )
                    {
                        this.LogIt( $"Received shutdown command... Shutting down..." );
                        this.Shutdown();
                        continue;
                    }
                    // test for special routes
                    else if ( this.SpecialRoutes.ContainsKey( context.Request.Url.LocalPath ) )
                    {
                        this.LogIt( $"Request for special route {context.Request.Url.LocalPath}" );
                        this.SpecialRoutes[context.Request.Url.LocalPath]( context );
                        continue;
                    }
                    // serve a physical HTML page
                    else if ( this.EnablePageServe )
                    {
                        // test the local path is the home path
                        string path = context.Request.Url.LocalPath.Trim().TrimStart( new char[] { '/' } );

                        // build a full file path
                        path = ( !String.IsNullOrWhiteSpace( path ) ) ? Path.Combine( this.RootFolder.FullName, path ) : Path.Combine( this.RootFolder.FullName, HomePage );

                        this.ServeWebFile( context.Response, path );
                        continue;
                    }
                    else // return 404
                    {
                        this.LogIt( $"An unknown or invalid request of '{context.Request.RawUrl}'." );
                        this.ServeError( context.Response, 404 );
                    }
                }
                catch (System.Exception exp)
                {
                    this.LogIt( exp.Message );
                    this.ServeError( context.Response, 500 );
                }
            }

            // normal shutdown
            return 0;
        }

        /// <summary>Test the response with a request in the format /test?echo=some test message</summary>
        /// <param name="response"></param>
        /// <param name="localPath"></param>
        protected void ServeTest( HttpListenerContext ctx )
        {
            // coalesce all values in the echo param 
            string[] values = ctx.Request.QueryString.GetValues("echo");
            string msg = ( values != null && values.Length > 0 ) ? String.Join(' ' , values) : $"/test = {DateTimeOffset.UtcNow}";

            this.ServeSimpleHTML( ctx.Response, msg);
        }

        /// <summary>Respond with the server signature</summary>
        /// <param name="response"></param>
        /// <param name="localPath"></param>
        protected void ServeSignature( HttpListenerContext ctx )
        {
            this.ServeSimpleHTML( ctx.Response, $"Raydreams Server {DateTimeOffset.UtcNow}" );
        }

        /// <summary>Respond with the server signature</summary>
        /// <param name="response"></param>
        /// <param name="localPath"></param>
        protected void ServeFavicon( HttpListenerContext ctx )
        {
            var response = ctx.Response;

            byte[] ico = File.ReadAllBytes( Path.Combine( this.RootFolder.FullName, "favicon.ico" ) );

            if ( ico == null || ico.Length < 1 )
            {
                this.LogIt( $"Asking for the favicon but there isn't one." );
                this.ServeError( response, 404 );
                return;
            }

            response.StatusCode = 200;
            response.ContentType = "image/x-icon";
            response.ContentLength64 = ico.Length;
            response.OutputStream.Write( ico, 0, ico.Length );
            response.Close();
        }

        /// <summary>Respond with a physical web based file</summary>
        /// <param name="response"></param>
        protected void ServeWebFile( HttpListenerResponse response, string localPath )
        {
            // get File Path Info
            FileInfo fi = new FileInfo( localPath );

            // validate or return 404
            if ( !fi.Exists || !Extensions.Contains( fi.Extension ) )
            {
                this.LogIt( $"File {fi.Name} not found" );
                this.ServeError(response, 404);
                return;
            }

            this.LogIt( $"Request for {fi.FullName}" );

            // hold the results
            byte[] buffer = null;

            // if its a markdown file it has be converted first
            if ( this.ConvertMarkdown != null && (fi.Extension == ".md" || fi.Extension == ".markdown") )
            {
                string md = File.ReadAllText( fi.FullName );
                string body = this.ConvertMarkdown( md );

                buffer = UTF8Encoding.UTF8.GetBytes( $"<!DOCTYPE html><html><head></head><body>{body}</body></html>" );
            }
            else
            {
                // read the entire file into memory
                // using FileStream fs = new FileStream( path, FileMode.Open, FileAccess.Read );
                buffer = File.ReadAllBytes( fi.FullName );
            }

            // set the response length
            response.ContentLength64 = buffer.Length;

            // create a response stream
            using Stream outStream = response.OutputStream;
            outStream.Write( buffer, 0, buffer.Length );

            // set the content type which we will assume for now is UTF-8 text encoding but should check the HTML file
            response.StatusCode = 200;

            // set the response type - replace with the MIME type mapper
            if ( fi.Extension == ".html" || fi.Extension == ".htm" )
                response.ContentType = HTMLType;
            else if ( fi.Extension == ".css" )
                response.ContentType = CSSType;
            else if ( fi.Extension == ".jpeg" || fi.Extension == ".jpg" )
                response.ContentType = JPEGType;
            else if ( fi.Extension == ".png" )
                response.ContentType = PNGType;
            else if ( fi.Extension == ".gif" )
                response.ContentType = GIFType;

            // respond
            response.Close();
        }

        /// <summary>Reponds with some dynamic HTML</summary>
        /// <param name="response"></param>
        /// <param name="message"></param>
        protected void ServeSimpleHTML( HttpListenerResponse response, string message )
        {
            if ( String.IsNullOrWhiteSpace( message ) )
                message = "&nbsp;";

            response.StatusCode = 200;
            string msg = $"<!DOCTYPE html><html><head></head><body><div>{message}</div></body></html>";
            byte[] msgBytes = UTF8Encoding.UTF8.GetBytes( msg );
            response.ContentLength64 = msgBytes.Length;
            response.OutputStream.Write( msgBytes, 0, msgBytes.Length );

            response.ContentType = "text/html; charset=utf-8";
            response.Close();
        }

        /// <summary>Respond with JSON</summary>
        /// <param name="response"></param>
        /// <param name="body"></param>
        protected void ServeJSON( HttpListenerResponse response, string json )
        {
            string msg = String.Empty;

            if ( String.IsNullOrWhiteSpace(json) )
            {
                response.StatusCode = 204;
            }
            else
            {
                response.StatusCode = 200;
                msg = json.Trim();
            }

            byte[] msgBytes = UTF8Encoding.UTF8.GetBytes( msg );
            response.ContentLength64 = msgBytes.Length;
            response.OutputStream.Write( msgBytes, 0, msgBytes.Length );
            response.ContentType = "application/json; charset=utf-8";
            response.Close();
        }

        /// <summary>Serve up an error code - usually 404 or 500</summary>
        /// <param name="response"></param>
        protected void ServeError( HttpListenerResponse response, int errorCode )
        {
            response.StatusCode = errorCode;
            response.ContentLength64 = 0;
            response.Close();
        }

        /// <summary>Add a logger here later</summary>
        /// <param name="msg"></param>
        protected virtual void LogIt( string msg )
        {
            if ( String.IsNullOrWhiteSpace( msg ) )
                return;
        }

        #endregion [ Methods ]
    }
}
