using System;
using System.Web;
using System.Collections;
using System.Xml;
using System.IO;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.Text;
  

namespace Net.Exse.Xsltparameters
{
	using Net.Exse.Timingutil;
	using Net.Exse.Commonutil;

	public sealed class Xsltparameters
	{
		private static Hashtable parameterCache = new Hashtable();
        private struct CacheObject
        {
            public object content;
            public DateTime mtime ;

            public CacheObject(object p1, DateTime p2)
            {
                content = p1;
                mtime = p2;
            }
            public CacheObject(object p1)
            {
                content = p1;
                mtime = DateTime.Now;
            }
        }

	
		private static Object lock1 = new Object();

		public static Hashtable getXsltParameters(string filepath1,string filepath2,string httphost)
		{
			DateTime filemtime = File.GetLastWriteTime(filepath2);

			// the lock synchronizes the access to the static members of this class, 
			// cause it may happen that multiple threads want to access this at the same time
			// which may cause to load the file more than one 

			lock(lock1)
			{
				if ( 
					parameterCache.Contains(httphost) && ((CacheObject)parameterCache[httphost]).mtime == filemtime
				)
				{
					return (Hashtable)((Hashtable)((CacheObject)parameterCache[httphost]).content).Clone();
				}
				else
				{
					Timingutil.start("read parameter file");
					XmlDocument doc = new XmlDocument();

					{

						XslTransform transformer = new XslTransform();
		                XmlDocument doc1 = new XmlDocument();
						doc1.Load(filepath1);
						transformer.Load(doc1.CreateNavigator(),null,null);
					
					
	                	XmlDocument inputDoc = new XmlDocument();
						inputDoc.Load(filepath2);
									
						XmlReader reader = null;
						reader = transformer.Transform(inputDoc, new XsltArgumentList(),(XmlResolver)null);
				
						XmlDocument doc2 = new XmlDocument();
						doc2.Load(reader);

						//Console.Error.WriteLine(doc2.InnerXml);

									
						transformer.Load(doc2.CreateNavigator(),null,null);

						XmlDocument tmpDoc = new XmlDocument();
						tmpDoc.AppendChild(tmpDoc.CreateNode(XmlNodeType.Element, "root", null));
						reader = transformer.Transform(tmpDoc, new XsltArgumentList(),(XmlResolver)null);
				
						doc.Load(reader);
						//Console.Error.WriteLine(doc.InnerXml);
					}

				
					Hashtable xsltParameters = new Hashtable();
		
			
					IEnumerator paramEnum = doc.GetElementsByTagName("parameters")[0].SelectSingleNode("global").SelectNodes("param").GetEnumerator();

					while (paramEnum.MoveNext())  
					{
						//Console.Error.WriteLine("loop param");
						xsltParameters.Add(((XmlNode)paramEnum.Current).Attributes["key"].InnerText,((XmlNode)paramEnum.Current).Attributes["value"].InnerText);
					}
					//XmlElement nodeList = (XmlElement)doc.GetElementsByTagName("parameters")[0];
					//Console.Error.WriteLine(nodeList.InnerXml);
					
					
					Console.Error.WriteLine(httphost);
					
					//Console.Error.WriteLine("scan2");
					
					IEnumerator paramEnum5 = doc.GetElementsByTagName("parameters")[0].SelectNodes("domaingroup").GetEnumerator();
					while (paramEnum5.MoveNext())  
					{
						//Console.Error.WriteLine("loop domaingroup");
					
						
						IEnumerator paramEnum2 = ((XmlNode)paramEnum5.Current).SelectNodes("domain").GetEnumerator();
						while (paramEnum2.MoveNext())  
						{
							//Console.Error.WriteLine("loop domains");
							if ( ((XmlNode)paramEnum2.Current).Attributes["name"].InnerText == httphost )
							{
								//Console.Error.WriteLine("hostmatch");


								IEnumerator paramEnum4 = ((XmlNode)paramEnum5.Current).SelectNodes("param").GetEnumerator();

								while (paramEnum4.MoveNext())  
								{
									//Console.Error.WriteLine("loop params in group");
									if(xsltParameters.Contains(((XmlNode)paramEnum4.Current).Attributes["key"].InnerText))
									{
										xsltParameters.Remove(((XmlNode)paramEnum4.Current).Attributes["key"].InnerText);
									}
									xsltParameters.Add(((XmlNode)paramEnum4.Current).Attributes["key"].InnerText,((XmlNode)paramEnum4.Current).Attributes["value"].InnerText);
								}
								
								IEnumerator paramEnum3 = ((XmlNode)paramEnum2.Current).SelectNodes("param").GetEnumerator();

								while (paramEnum3.MoveNext())  
								{
									//Console.Error.WriteLine("loop params in domain");
									
									if(xsltParameters.Contains(((XmlNode)paramEnum3.Current).Attributes["key"].InnerText))
									{
										xsltParameters.Remove(((XmlNode)paramEnum3.Current).Attributes["key"].InnerText);
									}
									
									xsltParameters.Add(((XmlNode)paramEnum3.Current).Attributes["key"].InnerText,((XmlNode)paramEnum3.Current).Attributes["value"].InnerText);
								}
								
							}
						}
						
					}
					
					
					if(parameterCache.Contains(httphost))
					{
						parameterCache.Remove(httphost);
					}
					CacheObject cacheObject = new CacheObject(xsltParameters,filemtime);
					parameterCache.Add(httphost,cacheObject);

					Timingutil.stop();

					return (Hashtable)xsltParameters.Clone();
				}
			}
		}

	}
}
