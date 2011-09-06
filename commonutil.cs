namespace Net.Exse.Commonutil
{
	using System;
	using System.IO;
	using System.Xml;
	using System.Web;
	using System.Text;
	using Net.Exse.Timingutil;
	
	public sealed class Commonutil
	{
		
		public static string ReadTextFile(string path)
		{
			FileStream fs = new FileStream(path,FileMode.Open,FileAccess.Read);
			StreamReader s= new StreamReader(fs);
		 	//this is the key: pre-allocate the StringBuilder
		 	System.Text.StringBuilder sb = new System.Text.StringBuilder((int)fs.Length );	
			char[] buf = new char[1024*8];	
			int br = s.Read(buf,0,buf.Length);
			while (br > 0)
			{
				sb.Append(buf,0,br);
				br = s.Read(buf,0,buf.Length);
			}
			return sb.ToString();
		} 
		
		
		public static string XmlDocumentToString(XmlDocument doc)
		{
			MemoryStream ms = new MemoryStream();
            XmlTextWriter writer = new XmlTextWriter(ms,System.Text.Encoding.UTF8);
            writer.Formatting = Formatting.None; // None / Indented;
            //writer.Namespaces = false;
            writer.Indentation = 1;

            doc.Save(writer);
            ms.Seek(0,SeekOrigin.Begin);
            //ASCIIEncoding ascii = new ASCIIEncoding();
 	        UTF8Encoding utf8 = new UTF8Encoding();
            return new String(utf8.GetChars(ms.ToArray()));
			
	//		return doc.InnerXml;
		}

		public static XmlDocument GetErrorDocument(string errormessage)
		{
			XmlDocument errorDoc = new XmlDocument();
									
			errorDoc.AppendChild(errorDoc.CreateProcessingInstruction("xml-stylesheet", "type='text/xsl' href='xsl/errorPage.xsl'"));
			XmlNode root = errorDoc.AppendChild(errorDoc.CreateNode(XmlNodeType.Element, "root", null));
			XmlNode node = root.AppendChild(errorDoc.CreateNode(XmlNodeType.Element, "message", null));

			node.InnerText = errormessage;
			
			Timingutil.info("Compile Error ! , generate ErrorDom");
			return errorDoc;
		}

		public static DateTime ConvertFromUnixTimestamp(double timestamp)
		{
		    DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
	    	return origin.AddSeconds(timestamp);
		}
		
		
		public static double ConvertToUnixTimestamp(DateTime date)
		{
		    DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
			TimeSpan diff = date - origin;
			return Math.Floor(diff.TotalSeconds);
		}																													

		public static string HtmlEncode( string text ) 
		{
			char[] chars = HttpUtility.HtmlEncode( text ).ToCharArray();
			StringBuilder result = new StringBuilder( text.Length + (int)( text.Length * 0.1 ) );
        
			foreach ( char c in chars ) 
			{
				int value = Convert.ToInt32( c );
				if ( value > 127 )
					result.AppendFormat("&#{0};",value); 
				else
					result.Append( c );
			}
                                                                
			return result.ToString();
		}		

		
	}
}
