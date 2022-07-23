using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raydreams.MiniServer
{
	/// <summary>String utility extensions</summary>
	public static class StringExtensions
	{
		/// <summary>Replace a token in the format {{TOKEN_NAME}} where the token name is all uppercase</summary>
		/// <param name="template">The template string with tokens in the aforementioned format</param>
		/// <param name="token">the token ID string which will be upper cased automatically</param>
		/// <param name="value">The value to replace with</param>
		/// <returns>The newly formatted string</returns>
		/// <remarks>Dont use with string interpolation to avoid confusion</remarks>
		public static string ReplaceToken( this string template, string token, string value )
		{
			if ( String.IsNullOrWhiteSpace( template ) )
				return string.Empty;

			if ( String.IsNullOrWhiteSpace( token ) || String.IsNullOrWhiteSpace( value ) )
				return template;

			return template.Replace( String.Format( "{{{{{0}}}}}", token.Trim().ToUpper() ), value ).Trim();
		}

		/// <summary>Replaces the title and body in the HTML template</summary>
		/// <param name="template">The HTML template to use which uses the tokens $BODY$ and $TITLE$</param>
		/// <param name="body">The body of the page</param>
		/// <param name="title">The HTML Page title</param>
		public static string FormatHTMLTemplate( this string template, string body, string title = null )
		{
			if ( String.IsNullOrWhiteSpace( template ) )
				return $"<html><body><div>{body}</div></body></html>";

			if ( String.IsNullOrWhiteSpace( body ) )
				body = "&nbsp;";

			string html = template.Replace( "$BODY$", body.Trim() );

			if ( !String.IsNullOrWhiteSpace( title ) )
				html = html.Replace( "$TITLE$", title );

			return html;
		}

		/// <summary>Truncates a string to the the specified length or less</summary>
		public static string Truncate( this string str, int length, bool trim = true )
		{
			// if greater than length
			if ( str.Length > length )
				return ( trim ) ? str.Trim().Substring( 0, length ) : str.Substring( 0, length );

			return trim ? str.Trim() : str;
		}
	}
}
