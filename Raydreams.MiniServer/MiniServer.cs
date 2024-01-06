using System.Net;
using System.Text;
using System.Reflection;

namespace Raydreams.Web;

/// <summary>Web Server Interface</summary>
public interface IWebServer
{
    Task<int> Serve();

    bool AddSpecialRoute( string route, Action<HttpListenerContext> callback );

    void Shutdown();
}

/// <summary>Portable mini web server</summary>
public class MiniServer
{
    #region [ Fields ]

    /// <summary>The server name</summary>
    public static readonly string ServerName = "Raydreams MiniServer";

    /// <summary>Default page to return when no file is specified on the root path</summary>
    public static readonly string HomePage = "index.html";

    /// <summary>The charset defaults to UTF-8</summary>
    public static readonly string DefaultCharSet = "charset=utf-8";

    /// <summary>Serves as a very simple HTML template with only Title and Body replacement tokens</summary>
    public static readonly string SimpleHTMLTemplate = @"<!DOCTYPE html><html lang=""en""><head><meta charset=""utf-8"" /><title>$TITLE$</title></head><body>$BODY$</body></html>";

    /// <summary>When flipped to true, this server will end</summary>
    private bool _shutdown = false;

    #endregion [ Fields ]

    #region [ Constructors ]

    /// <summary>Constructor</summary>
    /// <param name="port">Port to run on</param>
    /// <param name="wwwRoot">Path to the root of the physical web folder to server files from</param>
    public MiniServer( int port, string? wwwRoot = null )
    {
        this.RootFolder = !String.IsNullOrWhiteSpace(wwwRoot) ? new DirectoryInfo( wwwRoot ) : null;

        if ( this.RootFolder == null )
            this.EnableFileServe = false;

        // because some people still use port 3000
        this.Port = Math.Clamp(port, 1024, 65535 );

        // sink OnLog to a null logger by default
        this.OnLog += ( object sender, string msg, string lvl) => { };

        // built in special routes but can be replaced
        this.SpecialRoutes = new Dictionary<string, Action<HttpListenerContext>>()
        {
            // signature of the server
            { "/sig", this.ServeSignature },

            // a test page uses the format /test?echo=message
            { "/test", this.ServeTest }
        };
    }

    #endregion [ Constructors ]

    #region [ Properties ]

    /// <summary>Collection of custom routes</summary>
    private Dictionary<string, Action<HttpListenerContext>> SpecialRoutes;

    /// <summary>The physical root web server file path</summary>
    public DirectoryInfo? RootFolder { get; set; } = null;

    /// <summary>When set to false, no physical pages can ever be served - only special paths</summary>
    public bool EnableFileServe { get; set; } = true;

    /// <summary>port to listen on</summary>
    public int Port { get; set; } = 50005;

    /// <summary>Is the server still running</summary>
    public bool IsRunning => !this._shutdown;

    /// <summary>Delegate Function for converting Markdown to HTML. Input string is markdown and return is HTML.</summary>
    /// <returns>Converted HTML</returns>
    /// <remarks>Supply your own Markdown conversion routine</remarks>
    public Func<string, string> ConvertMarkdown { get; set; } = ( string md ) => "<h1>Markdown not supported!</h1>";

    #endregion [ Properties ]

    #region [ Events ]

    /// <summary>A log event raised when logs happen in the format {this object, log message, log level}</summary>
    /// <remarks>For now the only sent log levels are Info and Error.</remarks>
    public event Action<object, string, string> OnLog;

    #endregion [ Events ]

    #region [ Public Methods ]

    /// <summary>Explicit call to shutdown</summary>
    public void Shutdown()
    {
        this._shutdown = true;
    }

    /// <summary>Allows for adding a new special route at run time</summary>
    /// <param name="route">The custom route which optionally is prefxied with forward slash. Routes need to be case insensitive.</param>
    /// <param name="callback">The callback function when this route is called</param>
    /// <returns>True if added, otherwise false</returns>
    /// <remarks>You can replace existing routes by adding it again.</remarks>
    public bool AddSpecialRoute( string route, Action<HttpListenerContext> callback )
    {
        if ( String.IsNullOrWhiteSpace( route ) || callback == null )
            return false;

        route = route.Trim().ToLowerInvariant();

        if ( route[0] != '/' )
            route = $"/{route}";

        // special routes can be replaced at runtime
        if ( this.SpecialRoutes.ContainsKey( route ) )
            this.SpecialRoutes[route] = callback;
        else
            this.SpecialRoutes.Add( route, callback );

        return true;
    }

    /// <summary>Start serving files</summary>
    public async Task<int> Serve()
    {
        // check to see if we can start the server
        if ( !HttpListener.IsSupported )
            return -1;

        // setup server and listen on the following ports
        HttpListener server = new();
        server.Prefixes.Add( $"http://localhost:{Port}/" );
        server.Prefixes.Add( $"http://{IPAddress.Loopback}:{Port}/" );
        
        // start listening
        server.Start();

        this.LogMessage( $"Dr. Frasier Crane here... I'm Listening at {server.Prefixes.First()}" );

        // endless loop until force quit for now
        while ( this.IsRunning )
        {
            // block for a new request
            HttpListenerContext context = await server.GetContextAsync();

            try
            {
                string specialRoute = String.Empty;

                // special route paths have to be case insensitive
                if ( context.Request.Url != null )
                    specialRoute = context.Request.Url.LocalPath.ToLowerInvariant();

                // always check shutdown first - nothing can override it
                if ( specialRoute == "/shutdown" )
                {
                    this.LogMessage( $"Received shutdown command... Shutting down..." );
                    this.Shutdown();
                    continue;
                }
                // favicon route can not be changed
                else if ( specialRoute == "/favicon.ico" )
                {
                    this.ServeFavicon( context );
                }
                // special routes have next precedence
                else if ( this.SpecialRoutes.ContainsKey( specialRoute ) )
                {
                    this.LogMessage( $"Request for special route {specialRoute}" );
                    this.SpecialRoutes[specialRoute]( context );
                }
                // finally serve any physical HTML page if allowed
                else if ( this.EnableFileServe && this.RootFolder != null && context.Request.Url != null )
                {
                    // test the local path is the home path
                    string path = context.Request.Url.LocalPath.Trim().TrimStart( new char[] { '/' } );

                    // build a full file path
                    path = ( !String.IsNullOrWhiteSpace( path ) ) ? Path.Combine( this.RootFolder.FullName, path ) : Path.Combine( this.RootFolder.FullName, HomePage );

                    this.ServeWebFile( context.Response, path );
                }
                else // return 404
                {
                    this.LogError( $"An unknown or invalid request of '{context.Request.RawUrl}'." );
                    this.ServeError( context.Response, 404 );
                }
            }
            catch (System.Exception exp)
            {
                this.LogError( exp.ToLogMsg(true) );
                this.ServeError( context.Response, 500 );
            }
        }

        // normal shutdown
        return 0;
    }

    #endregion [ Public Methods ]

    #region [ Methods ]

    /// <summary>Test the response with a request in the format /test?echo=some test message</summary>
    /// <param name="response"></param>
    /// <param name="localPath"></param>
    protected void ServeTest( HttpListenerContext ctx )
    {
        // coalesce all values in the echo param 
        string[]? values = ctx.Request.QueryString.GetValues("echo");

        string msg = ( values != null && values.Length > 0 ) ? String.Join(' ' , values) : $"/test = {DateTimeOffset.UtcNow}";

        this.ServeSimpleHTML( ctx.Response, msg);
    }

    /// <summary>Respond with the server signature</summary>
    /// <param name="response"></param>
    /// <param name="localPath"></param>
    protected void ServeSignature( HttpListenerContext ctx )
    {
        this.ServeSimpleHTML( ctx.Response, $"{ServerName} {GetVersion()} {DateTimeOffset.UtcNow}" );
    }

    /// <summary>Respond with the favicon in the root of the web folder</summary>
    /// <param name="response"></param>
    /// <param name="localPath"></param>
    protected void ServeFavicon( HttpListenerContext ctx )
    {
        var response = ctx.Response;

        if ( this.RootFolder == null || !this.RootFolder.Exists)
            return;

        byte[] ico = File.ReadAllBytes( Path.Combine( this.RootFolder.FullName, "favicon.ico" ) );

        if ( ico == null || ico.Length < 1 )
        {
            this.LogError( $"Asking for the favicon but there isn't one." );
            this.ServeError( response, 404 );
            return;
        }

        response.StatusCode = 200;
        response.ContentType = MimeTypeMap.GetMimeType(".ico");
        response.ContentLength64 = ico.Length;
        response.OutputStream.Write( ico, 0, ico.Length );
        response.Close();
    }

    /// <summary>Respond with a physical web based file</summary>
    /// <param name="response"></param>
    protected void ServeWebFile( HttpListenerResponse response, string localPath )
    {
        // get File Path Info
        FileInfo fi = new ( localPath );

        // validate or return 404
        if ( !fi.Exists || !MimeTypeMap.Supported( fi.Extension ) )
        {
            this.LogError( $"File {fi.Name} not found" );
            this.ServeError(response, 404);
            return;
        }

        this.LogMessage( $"Request for {fi.FullName}" );

        // hold the results
        byte[] buffer;

        // if its a markdown file it has be converted first
        if ( this.ConvertMarkdown != null && (fi.Extension == ".md" || fi.Extension == ".markdown") )
        {
            string md = File.ReadAllText( fi.FullName );
            string body = this.ConvertMarkdown( md );
            body = SimpleHTMLTemplate.FormatHTMLTemplate( body, Path.GetFileNameWithoutExtension( fi.Name ) );
            buffer = UTF8Encoding.UTF8.GetBytes( body );
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
        response.KeepAlive = false;

        // set the content type which we will assume for now is UTF-8 text encoding but should check the HTML file
        response.StatusCode = 200;

        // set the response type - text types need the charset
        string mimeType = MimeTypeMap.GetMimeType( fi.Extension );
        response.ContentType = MimeTypeMap.IsText( mimeType ) ? $"{mimeType}; {DefaultCharSet}" : mimeType;

        // respond
        response.Close();
    }

    /// <summary>Reponds with some dynamic HTML inserted into a simple HTML block</summary>
    /// <param name="response"></param>
    /// <param name="message"></param>
    protected void ServeSimpleHTML( HttpListenerResponse response, string body )
    {
        string msg = SimpleHTMLTemplate.FormatHTMLTemplate( body, ServerName );
        byte[] msgBytes = UTF8Encoding.UTF8.GetBytes( msg );

        response.StatusCode = 200;
        response.KeepAlive = false;
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
        response.KeepAlive = false;
        response.ContentLength64 = msgBytes.Length;
        response.OutputStream.Write( msgBytes, 0, msgBytes.Length );
        response.ContentType = "application/json; charset=utf-8";
        response.Close();
    }

    /// <summary>Serve up an error code - usually 404 or 500</summary>
    /// <param name="response"></param>
    protected virtual void ServeError( HttpListenerResponse response, int errorCode )
    {
        response.StatusCode = errorCode;
        response.KeepAlive = false;
        response.ContentLength64 = 0;
        response.Close();
    }

    /// <summary>Gets the version of THIS assembly</summary>
    /// <returns>The vesion as a string</returns>
    public static string GetVersion()
    {
        var assName = Assembly.GetExecutingAssembly().GetName();
        return ( assName.Version != null) ? assName.Version.ToString() : "0.0.0";
    }

    #endregion [ Methods ]

    #region [ Logging ]

    /// <summary>Log an Info Message</summary>
    /// <param name="msg"></param>
    protected virtual void LogMessage( string msg )
    {
        if ( String.IsNullOrWhiteSpace( msg ) )
            return;

        if ( this.OnLog != null )
            this.OnLog( this, msg, "Info" );
    }

    /// <summary>Log an Error Message</summary>
    /// <param name="msg"></param>
    protected virtual void LogError( string msg )
    {
        if ( String.IsNullOrWhiteSpace( msg ) )
            return;

        if ( this.OnLog != null )
            this.OnLog( this, msg, "Error" );
    }

    #endregion [ Logging ]
}
