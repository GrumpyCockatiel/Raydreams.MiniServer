# C# Mini Web Server

Make your own little C# .NET Core web server. This is useful when you need to spawn an in-app server to listen to redirects on localhost.

The primary use cases are:

* intercepting redirects from an OAuth Callback
* serving your own custom files like custom encrypted files

It only supports a single web site hosted in some local folder.

You can subclass it to add custom route handlers for specific callback URLs as shown in this example.

For now it only handles custom routes, HTML files, Javascript, CSS, JSON, and basic images.

You can add in Markdown support (demo included) to serve Markdown as HTML using something like **Markdig**.

# What It's Not (for now)

While you can serve physical files as a stand alone exe, that's not really the use case and MiniServer is primarily 

## nuget

You can download the [nuget package](https://www.nuget.org/packages/raydreams.miniserver/) from nuget.org

## HOW TO

It's best to first subclass Mini Server

```
public class MiniMeServer : MiniServer
{
    public MiniMeServer( int port, string rootPath ) : base(port, rootPath)
    {
        // add more custom routes here
        this.AddSpecialRoute( "/json", ServeJSONExample );
    }

    /// <summary>Example on how to add custom routes</summary>
    protected void ServeJSONExample( HttpListenerContext ctx )
    {
        var jsonObj = new { Timestamp = DateTimeOffset.UtcNow, Message = "My Special Object" };

        // add a event callback to your own code here when this happens

        base.ServeJSON( ctx.Response, JsonConvert.SerializeObject( jsonObj ) );
    }
}
```

Then you can add your own special routes to handle custom localhost path's useful for something like an OAuth redirect.

You can supply any method to handle conversion from Markdown to HTML using the delegate property

```
public delegate string MarkdownToHTML( string md );

MiniMeServer server = new MiniMeServer( 50001, Path.Combine( WebRoot, WebFolder ) )
    { ConvertMarkdown = new Markdigdowner().GetHtml };
```

# Testing

Run the server and browse to `http://localhost:50001/test?echo=hello%20world`

## 301/302 Redirect

There's a lot of misunderstanding about what a HTTP 301/302 Redirect Response Codes really mean. HTTP is **always** a Request followed by a Response - and that's it! Getting a 301 or 302 is like calling a number and getting a message that the person you are calling is no longer at that number but here is their new number. You then have to hang-up and make a new call. Your call is NOT forwarded automatically. Yes, a proxy server may forward your call and make anther HTTP Request, and so and so on, but eventually (hopefully) all those chained Requests come back and ultimately responds to your orginal Request with a 20x code.

## OAuth

So how is this useful with OAuth? Well, An OAuth client has a callback URL which you can set to something like

```
http://localhost:50001/token?code=XYZpdq123
```

By adding a custom route to `/token`, when OAuth redirects after a user logins to their account on the Identity Management site, they will be sent back to Mini Server where you can intercept the response and extract the token or code and store it locally. Of course, Mini Server needs to be running before they log in, but you can shut it down as soon everything returns.

Add an event to your own subclass your main code will listen to.


## Not Implemented

* HTTPS - there's no support for HTTPS.
* Multiple Requests - Only one request can be handled at a time. Other incoming requests are blocked until processing requests are complete.
* Binding to custom domains.
* Logging - better logger interface coming soon.
* File Parsing - there's no real plan to parse physical files as that sort of defeats the purpose of a light weight web server, but never say never.