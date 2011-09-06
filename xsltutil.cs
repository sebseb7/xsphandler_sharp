namespace Net.Exse.Xsltutil
{
	using Net.Exse.Timingutil;
	using Net.Exse.Commonutil;
	using System;
	using System.Collections;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Xml;
	using System.IO;
	using System.Xml.XPath;
	using System.Xml.Xsl;

	public class XsltUtilReturnObject
	{
		public XmlDocument document;
		public string debugcontent;
	}


	public sealed class Xsltutil
	{
	
		private struct CacheObject
		{
			public object content;
			public DateTime mtime ;
											
			public CacheObject(object p1, DateTime p2)
			{
				content = p1;
				mtime = p2;
			}
		}
		
		private static Hashtable stylesheetCache = new Hashtable();
		private static Regex XmlPiRegex = new Regex(@"href=\'(?'Url'[^\']+)\'");
		

		private static void CacheStyleSheet(string filepath,Hashtable xsltParameters)
		{
			try
			{
				XslCompiledTransform transformer2 = new XslCompiledTransform();


				XmlTextReader reader = new XmlTextReader(filepath);
				reader.WhitespaceHandling = WhitespaceHandling.All;
				
				
            	XmlDocument doc = new XmlDocument();
				doc.PreserveWhitespace=true;
	            doc.Load(reader);

				/*XmlNode node = doc.FirstChild.NextSibling.NextSibling;

				IDictionaryEnumerator xsltParameterEnum = xsltParameters.GetEnumerator();
				while ( xsltParameterEnum.MoveNext() )
				{
					XmlNode paramNode = doc.CreateNode(XmlNodeType.Element, "xsl:param", "http://www.w3.org/1999/XSL/Transform");

				    XmlAttribute newAttr1 = doc.CreateAttribute("name");
				    newAttr1.Value = (string)xsltParameterEnum.Key;
					paramNode.Attributes.Append(newAttr1);

				    XmlAttribute newAttr2 = doc.CreateAttribute("select");
				    newAttr2.Value = "'__undefined__'";
					paramNode.Attributes.Append(newAttr2);

					node.InsertBefore(paramNode, node.FirstChild);
				}

				Console.Error.Write(Commonutil.XmlDocumentToString(doc));*/
				transformer2.Load(doc.CreateNavigator(),null,null);

				DateTime filemtime = File.GetLastWriteTime(filepath);
				CacheObject cacheObject = new CacheObject(transformer2,filemtime);
				stylesheetCache.Add(filepath,cacheObject);
			}
			catch (System.Xml.XmlException e)
			{
				throw new XsltutilException("Parse Stylesheet unsuccessful:"+e.Message);
			}
			catch (System.Xml.Xsl.XsltCompileException e)
			{
				throw new XsltutilException("Compile Stylesheet unsuccessful:"+e.Message);
			}
		}

		private static string getStylesheetUrl(XmlDocument inputDoc)
		{
			string piContent="";
			if (inputDoc.HasChildNodes)
			{
				for (int i=0; i<inputDoc.ChildNodes.Count; i++)
				{
					if (inputDoc.ChildNodes[i].NodeType == XmlNodeType.ProcessingInstruction)
					{
						if ( "xml-stylesheet" == ((XmlProcessingInstruction)inputDoc.ChildNodes[i]).Target)
						{
						piContent = ((XmlProcessingInstruction)inputDoc.ChildNodes[i]).Data;
						}
					}
				}
			}
            foreach(Match match in XmlPiRegex.Matches(piContent) )
            {
                return match.Groups["Url"].Captures[0].Value;
            }
			return null;
		}


		public static XsltUtilReturnObject doTransform(XmlDocument doc,Hashtable xsltParameters,string basepath,string rootpath)
		{
			Timingutil.start("createParameterList");
      		//we should cache that list if the hashvalue of the request is identical
			XsltArgumentList xslArg = new XsltArgumentList();
			IDictionaryEnumerator xsltParameterEnum = xsltParameters.GetEnumerator();
			while ( xsltParameterEnum.MoveNext() )
			{
				xslArg.AddParam((string)xsltParameterEnum.Key, "", (string)xsltParameterEnum.Value);
			}
			
			Exslt obj = new Exslt();
		    xslArg.AddExtensionObject("http://www.matchperfect.com/exslt", obj);
			
			Timingutil.stop();
			
			string filepath = null;
			
			filepath = getStylesheetUrl(doc);

			if(filepath != null)
			{
				if(filepath.StartsWith("/"))
				{
					filepath = rootpath+filepath;
				}
				else
				{
					filepath = basepath+"/"+filepath;
				}
			
				filepath = Path.GetFullPath(filepath);

				if(filepath == basepath+"/")
				{
					filepath = null;
				}

			}

			string debugcontent ="";

			
			while(filepath != null)
			{
				//debugcontent += Commonutil.XmlDocumentToString(doc)+"\n - - - \n";
				doc = transformStage(doc,filepath,xsltParameters,xslArg);
				basepath = Directory.GetParent(filepath).ToString()+"/";

				filepath = getStylesheetUrl(doc);

				if(filepath != null)
				{
					if(filepath.StartsWith("/"))
					{
						filepath = Path.GetFullPath(rootpath+filepath);
					}
					else
					{
						filepath = Path.GetFullPath(basepath+"/"+filepath);
					}
				}

				//?? what is the reason of this ? 
				if(filepath == basepath)
				{
					filepath = null;
				}
			}
		
			XsltUtilReturnObject returnObject = new XsltUtilReturnObject();
			returnObject.document = doc;
			returnObject.debugcontent= debugcontent;

			return returnObject ;
		}

		// we want to ensure that the stylesheet cache is looked
		// this brings no performance problems, cause we only lock
		// during the lookup und the loading/compiling , without this lock it may
		// happen that the same stylesheet gets cached twice in parrallel
		
		private static Object stylesheetCacheLock = new Object();
		
		private static XmlDocument transformStage(XmlDocument inputDoc,string filepath,Hashtable xsltParameters,XsltArgumentList xslArg)
		{
			if(! File.Exists(filepath))
			{
				Timingutil.info("ss not found :'"+filepath+"'");
				//Timingutil.stop();
				Console.Error.WriteLine("Stylesheet "+filepath+" not found");
				throw new XsltutilException("Stylesheet "+filepath+" not found");
			}
		
			Timingutil.start("transform stage("+filepath+")");

			XslCompiledTransform transformer = null;
			
			DateTime filemtime = File.GetLastWriteTime(filepath);
			
			// we dont want multiple thread to access the cache
			lock(stylesheetCacheLock)
			{
				if (stylesheetCache.Contains(filepath))
				{
					if( ((CacheObject)stylesheetCache[filepath]).mtime != filemtime )
					{
						Timingutil.start("loadStylesheet (changed)");
						stylesheetCache.Remove(filepath);
						CacheStyleSheet(filepath,xsltParameters);
						Timingutil.stop();
					}
				}
				else
				{
					Timingutil.start("loadStylesheet (new)");
					CacheStyleSheet(filepath,xsltParameters);
					Timingutil.stop();
				}
				transformer = (System.Xml.Xsl.XslCompiledTransform)((CacheObject)stylesheetCache[filepath]).content;
			}

			
			
			XmlDocument outxmldoc = new XmlDocument();
			XmlWriter writer = outxmldoc.CreateNavigator().AppendChild();

//			bool transformSucceeded = false;
			try
			{
				Timingutil.start("transform");
				transformer.Transform(inputDoc, xslArg,writer);
				writer.Close();
				Timingutil.stop();
//				transformSucceeded = true;
			}
			catch (System.Xml.XPath.XPathException e)
			{
				Console.Error.WriteLine("Xpath Transformation error in '"+filepath+"':"+e.Message);
				throw new XsltutilException("Xpath Transformation error in '"+filepath+"':"+e.Message);
			}

			Timingutil.stop();

			return outxmldoc;
			
		}
	}
	public class Exslt
	{
		public string ts(int textid)
		{
			return "";
    	}
		public string te()
		{
			return "";
    	}

		public string escape(string text)
		{
			StringBuilder sb = new StringBuilder();
			for(int i = 0 ; i < text.Length; i++)
			{
				int charcode = (int)text[i];
				if( charcode > 127)
				{
					sb.Append("%"+String.Format("{0:X}",charcode));
				}
				else
				{
					sb.Append(text[i]);
				}
			}
			return sb.ToString();
    	}
		
		public System.Xml.XPath.XPathNavigator putCache(System.Xml.XPath.XPathNavigator tree)
		{

			return tree;
		}
		
  	}

	public class XsltutilException : Exception
    {
        public string s;
        public XsltutilException():base()
        {
            s=null;
        }
        public XsltutilException(string message):base()
        {
            s=message.ToString();
        }
        public XsltutilException(string message,Exception myNew):base(message,myNew)
        {
            s=message.ToString();// Stores new exception message into class member s
        }
    }


}		