namespace Net.Exse.WebHandler
{
	using System;
	using System.Web;
	using System.Xml;
	using System.IO;
	using System.Text;
	using System.Diagnostics; // for Process
	using System.Collections;
	using System.Collections.Specialized;
	using System.Reflection;
	using System.CodeDom.Compiler;
	using Microsoft.CSharp;
	using System.Web.SessionState;
	using System.Text.RegularExpressions;
	using Net.Exse.Timingutil;
	using Net.Exse.Xsltutil;
	using Net.Exse.Commonutil;
	using Net.Exse.Xsltparameters;

	class WebHandler : IHttpHandler , System.Web.SessionState.IRequiresSessionState
	{
		// the relative path from the handler dir to the htdocs dir (must not begin with an '/')
	
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
	
		private static Hashtable codefileCache = new Hashtable();
		private static Object codefileCacheLock = new Object();
		private static Hashtable imageCache = new Hashtable();
		private static Object imageCacheLock = new Object();
		private static Object rsvgProcessLock = new Object();
		private System.Security.Cryptography.MD5CryptoServiceProvider Md5Provider = new System.Security.Cryptography.MD5CryptoServiceProvider();
		private static System.Security.Cryptography.MD5CryptoServiceProvider Md5Provider2 = new System.Security.Cryptography.MD5CryptoServiceProvider();
	
		public void ProcessRequest (HttpContext context)
		{
			Timingutil.init();
	
			HttpResponse resp = context.Response;
			HttpRequest req = context.Request;
			HttpSessionState session = context.Session;

/*
		{
		int loop1, loop2;
		NameValueCollection coll;
		
		coll=req.Params;//ServerVariables; 
		String[] arr1 = coll.AllKeys; 
		for (loop1 = 0; loop1 < arr1.Length; loop1++) 
		{
		   Console.Error.WriteLine("Key: " + arr1[loop1] + "<br>");
	      String[] arr2=coll.GetValues(arr1[loop1]);
		     for (loop2 = 0; loop2 < arr2.Length; loop2++) {
			       Console.Error.WriteLine("Value " + loop2 + ": " + arr2[loop2] + "<br>");
		      }
		  }
		}
*/

			string rootpath = Path.GetFullPath(req.PhysicalApplicationPath);
			string basepath = Path.GetFullPath(Directory.GetParent(req.PhysicalPath).ToString());


			Timingutil.start("getXsltParameters");
			// eleminate this ".." try to find the docroot
			Hashtable xsltParameters = Xsltparameters.getXsltParameters(req.PhysicalApplicationPath+"/bin/xsltparameters.xslt",rootpath+"/xsltParameters.xml",req.ServerVariables["HTTP_HOST"]);
			Timingutil.stop();

			
			

			resp.ContentType = "text/html";
		
			string filename = Path.GetFullPath(basepath+"/"+Path.GetFileName(req.PhysicalPath).ToString());

			

			DateTime filemtime = File.GetLastWriteTime(filename);

			//Console.Error.WriteLine("basepath:"+basepath);
			//Console.Error.WriteLine("filename:"+filename);


			XmlDocument inputDoc;

			try
			{
				object loObject;


				lock(codefileCacheLock)
				{
					try
					{
						Timingutil.start("getPageObject");
						if (codefileCache.Contains(filename))
						{
							if(((CacheObject)codefileCache[filename]).mtime != filemtime)
							{
								codefileCache.Remove(filename);
								CacheFile(filename,req);
							}
						}
						else
						{
							CacheFile(filename,req);
						}
						loObject = ((CacheObject)codefileCache[filename]).content;
					}
					catch(CompileException e)
					{
						throw;
					}
					finally
					{
						Timingutil.stop();
					}
				}
			


			
			
				object[] loCodeParms = new object[3];
				loCodeParms[0] = req;
				loCodeParms[1] = session;
				loCodeParms[2] = xsltParameters;
		
				object loResult;		
		//		try
		//		{
					Timingutil.start("executePage");
					loResult = loObject.GetType().InvokeMember("Query",BindingFlags.InvokeMethod,null,loObject,loCodeParms);
		//		}
		//		catch (Exception e)
		//		{
		//			throw;
		//		}
		//		finally
		//		{
					Timingutil.stop();
		//		}
				
				if(loResult.GetType().ToString() == "System.String")
				{
					resp.ContentType = "application/vnd.ms-excel";
//					resp.ContentType = "text/plain";
					resp.Write((string)loResult);
					return;
				}
				if(loResult.GetType().ToString() == "System.Byte[]")
				{
					resp.ContentType = "image/png";
					resp.BinaryWrite((byte[])loResult);
					resp.Flush();
					return;
				}
				else
				{
					inputDoc = (XmlDocument)loResult;
				}

			}
			catch(CompileException e)
			{
				Console.Error.WriteLine("compile Exception:"+e.s);
				inputDoc = Commonutil.GetErrorDocument(e.s);
			}
			catch(Exception e)
			{
				//Console.Error.WriteLine("Runtime Exception:"+e.ToString());
				inputDoc = Commonutil.GetErrorDocument("Runtime Exception:"+e.ToString());
			}


            string redirectContent="";
			if (inputDoc.HasChildNodes)
			{
				for (int i=0; i<inputDoc.ChildNodes.Count; i++) 
				{
					if (inputDoc.ChildNodes[i].NodeType == XmlNodeType.ProcessingInstruction)
					{
						if ( "http-redirect" == ((XmlProcessingInstruction)inputDoc.ChildNodes[i]).Target)
						{
							redirectContent = ((XmlProcessingInstruction)inputDoc.ChildNodes[i]).Data;
						}
					}
				}
			}
			
			if (redirectContent != "")
			{
				Timingutil.stop();
				resp.Redirect(redirectContent,false);
				// if reguest logging is activated log redirect here
			}
			else
			{
			
				Timingutil.start("transform");
			
				XsltUtilReturnObject result = null;
				try
				{
					result = Xsltutil.doTransform(inputDoc,xsltParameters,basepath,rootpath);
				}
				catch(XsltutilException e)
				{
					//Console.Error.WriteLine("xsltutil Exception:"+e.s);
					inputDoc = Commonutil.GetErrorDocument(e.s);
					
					try
					{
						result = Xsltutil.doTransform(inputDoc,xsltParameters,basepath,rootpath);
					}
					catch(XsltutilException e2)
					{	
						throw;
					}
				}
				if(result == null)
				{
					//Console.Error.WriteLine("result Doc is Null");
					inputDoc = Commonutil.GetErrorDocument("result Doc is Null");
					result = Xsltutil.doTransform(inputDoc,xsltParameters,basepath,rootpath);
				}


				XmlDocument resultDoc = result.document;

				Timingutil.stop();

				Timingutil.start("output");
				

				string docType = "text/html" ; // the default;
				
				{
					Timingutil.start("regex_1");
					
					XmlNode piNode = (XmlProcessingInstruction)resultDoc.SelectSingleNode("/processing-instruction(\"output-format\")");
					
					if (piNode != null)
					{
						string pi = piNode.Value;
						Regex regex = new Regex("type=\"(?'g1'[^\"]*)\"");
						foreach(Match match in regex.Matches(pi) )
						{
							docType=match.Groups["g1"].Captures[0].Value;
						}
						((XmlNode)resultDoc).RemoveChild(piNode);
						Timingutil.info("doctype "+docType);
					}else{
						Timingutil.info("default doctype (text/html)");
					}
					Timingutil.stop();
				}

				resp.ContentType = docType;

				if (docType == "text/html")
				{
					Timingutil.start("tostring");
					string string1 = Commonutil.XmlDocumentToString(resultDoc);
					//Console.Error.WriteLine('a'+string1);
					string1 = string1.Remove(0,string1.IndexOf("<"));
					Timingutil.stop();
					{
						
				/*		Timingutil.start("regex");
						Regex regex;
						//string1=string1.Replace("&lt;","<");
						//string1=string1.Replace("&gt;",">");
						regex = new Regex(@"&amp;");
						string1=regex.Replace(string1,"&");
						Timingutil.stop();
				*/		
					}
					string1 = string1.Insert(string1.IndexOf("<html>"),"\n<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/2002/REC-xhtml1-20020801/DTD/xhtml1-transitional.dtd\">\n");
					string1 = string1.Replace("<html>","<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"en\" lang=\"en\">");
					//string1 = string1.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>","");
					resp.Write(string1);
					Timingutil.stop();
					//resp.Write("\n<!--\n100 = 1ms\n\n"+Timingutil.dump()+"\n\n-->");
					if(session["lasttiming"] != null)
					{
						//resp.Write("<pre>LAST:"+session["lasttiming"]+"</pre>");
						session["lasttiming"] = null;
					}
					
					//resp.Write("<pre align=\"left\" text-align=\"left\">"+Timingutil.dump()+"</pre>");
					//resp.Write("<!-- debug:"+result.debugcontent+"-->");
				}

				if (docType == "text/css")
				{
					if (inputDoc.HasChildNodes)
					{
						for (int i=0; i<inputDoc.ChildNodes.Count; i++) 
						{
							if (inputDoc.ChildNodes[i].NodeType == XmlNodeType.ProcessingInstruction)
							{
								if ( "output-header" == ((XmlProcessingInstruction)inputDoc.ChildNodes[i]).Target)
								{
									string headerstr = ((XmlProcessingInstruction)inputDoc.ChildNodes[i]).Data;
									string headercontent = headerstr.Remove(0,headerstr.IndexOf(":")+2);
									string headername = headerstr.Remove(headerstr.IndexOf(":"),headerstr.Length-(headerstr.IndexOf(":")));
									
									resp.AddHeader(headername,headercontent);
									
								}
							}
						}
					}


				//	XmlTextWriter writer = new XmlTextWriter (Console.Error);
				 //   writer.Formatting = Formatting.Indented;
			//		writer.WriteNode(resultDoc.DocumentElement, false);
			//		writer.Close();
					string string1 = resultDoc.DocumentElement.InnerXml;
					resp.Write(string1);
					Timingutil.stop();
				}
				
				if (docType == "text/xml")
				{
					string string1 = resultDoc.InnerXml;
					resp.Write(string1);
//					Timingutil.stop();
					session["lasttiming"]=Timingutil.dump();
				}

				if (docType == "text/plain")
				{
					string string1 = resultDoc.InnerXml;
					resp.Write(string1);
					Timingutil.stop();
				}

				if ((docType == "image/png")||(docType == "image/jpeg"))
				{

					string string1 = resultDoc.InnerXml;
				
					byte[] hashdata = System.Text.Encoding.ASCII.GetBytes(string1);
					hashdata = Md5Provider.ComputeHash(hashdata);
					string hashvalue = "";
					for (int i=0; i < hashdata.Length; i++) hashvalue += hashdata[i].ToString("x2").ToLower();

					/*
						TODO: remove that tmeporary files
					*/
				
					string fileext;
					if(docType == "image/png")
					{
						fileext = "png";
					}
					else
					{
						fileext = "jpeg";
					}
				
					lock(imageCacheLock)
					{
						if (imageCache.Contains(hashvalue))
						{
							resp.BinaryWrite((byte[])((CacheObject)imageCache[hashvalue]).content);
						}
						else
						{
							lock(rsvgProcessLock)
							{
								TextWriter tw = new StreamWriter("/tmp/_image.svg");
								tw.Write(string1);
								tw.Close();	
					
								Process compiler = new Process();
								compiler.StartInfo.FileName = "/usr/bin/rsvg";
								compiler.StartInfo.Arguments = "/tmp/_image.svg /tmp/_image.png";
								compiler.StartInfo.UseShellExecute = false;
								compiler.Start();
								compiler.WaitForExit();
						
								if(fileext == "jpeg")
								{
									Process converter = new Process();
									converter.StartInfo.FileName = "/usr/bin/convert";
									converter.StartInfo.Arguments = "/tmp/_image.png /tmp/_image.jpeg";
									converter.StartInfo.UseShellExecute = false;
									converter.Start();
									converter.WaitForExit();
								}
					
								FileStream fs = new FileStream("/tmp/_image."+fileext, FileMode.OpenOrCreate,FileAccess.Read);
								byte[] MyData= new byte[fs.Length];
								fs.Read(MyData, 0, System.Convert.ToInt32(fs.Length));
								fs.Close();
								
								CacheObject cacheObject = new CacheObject(MyData);
								imageCache.Add(hashvalue,cacheObject);
									
								resp.BinaryWrite(MyData);
							}
						}
					}

					Timingutil.stop();
				}

            
			}

		}


		private string CompileLib(String filename)
		{
				DateTime filemtime = File.GetLastWriteTime(filename);


				string returnModuleName = null;

				string directory = "/tmp/www-data-temp-aspnet-0/";
				string [] fileEntries_tmp = Directory.GetFiles(directory);
				foreach(string fileEntry in fileEntries_tmp)
				{
					if (fileEntry.EndsWith(".mdb")) continue;

					if ((fileEntry.Length > (33+directory.Length))&&((fileEntry.Substring(directory.Length,32) == GetMD5(filename))))
					{
						string moduleName = directory+GetMD5(filename)+"_"+Commonutil.ConvertToUnixTimestamp(filemtime).ToString()+".dll";
						
						if(moduleName == fileEntry)
						{
							Console.Error.WriteLine("use assembly for(1) "+filename);
							Timingutil.info("reuse : "+filename);
							returnModuleName = moduleName;
						}
						else
						{
							Timingutil.info("deleting old");
							File.Delete(fileEntry);
							File.Delete(fileEntry+".mdb");
						}
					}
				}	




				if(returnModuleName == null)
				{
					CodeDomProvider loCompiler = CodeDomProvider.CreateProvider("CSharp");
					//ICodeCompiler loCompiler = new CSharpCodeProvider().CreateCompiler();
					CompilerParameters loParameters = new CompilerParameters();
					loParameters.ReferencedAssemblies.Add("System.dll");
					loParameters.ReferencedAssemblies.Add("System.Data.dll");
					loParameters.ReferencedAssemblies.Add("Npgsql.dll");
					loParameters.ReferencedAssemblies.Add("System.Web.dll");
					loParameters.ReferencedAssemblies.Add("System.Drawing.dll");
					loParameters.GenerateInMemory = false;
					loParameters.CompilerOptions = "-debug";
					loParameters.IncludeDebugInformation = true;
					string moduleName = directory+GetMD5(filename)+"_"+Commonutil.ConvertToUnixTimestamp(filemtime).ToString()+".dll";
					loParameters.OutputAssembly = moduleName;

					Console.Error.WriteLine("compile local lib "+filename);

					Timingutil.start("compile lib"+filename);
					CompilerResults loCompiled = loCompiler.CompileAssemblyFromFile(loParameters,filename);
					Timingutil.stop();
		
					if (loCompiled.Errors.HasErrors)    
					{
						string lcErrorMsg = "";
						lcErrorMsg = loCompiled.Errors.Count.ToString() + " Errors:";
						for (int x=0;x<loCompiled.Errors.Count;x++) 
							lcErrorMsg = lcErrorMsg  + "\r\nLine: " +
						loCompiled.Errors[x].Line.ToString() + " - " +
						loCompiled.Errors[x].ErrorText;
					
					
						throw new CompileException("Errors compiling '"+filename+"':\n\n"+lcErrorMsg);
					}
					returnModuleName = moduleName;
		
				}
				return returnModuleName;
		}


		private void CacheFile(String filename,HttpRequest req)
		{
//				string lcCode = Commonutil.ReadTextFile(filename);

				DateTime filemtime = File.GetLastWriteTime(filename);

				Assembly loAssembly = null;


				string directory = "/tmp/www-data-temp-aspnet-0/";
				string [] fileEntries_tmp = Directory.GetFiles(directory);
				foreach(string fileEntry in fileEntries_tmp)
				{
					if (fileEntry.EndsWith(".mdb")) continue;

					if ((fileEntry.Length > (33+directory.Length))&&((fileEntry.Substring(directory.Length,32) == GetMD5(filename))))
					{
						string moduleName = directory+GetMD5(filename)+"_"+Commonutil.ConvertToUnixTimestamp(filemtime).ToString();
						
						if(moduleName+".dll" == fileEntry)
						{
							FileStream fs1 = new FileStream(moduleName+".dll", FileMode.Open,FileAccess.Read);
							FileStream fs2 = new FileStream(moduleName+".dll.mdb", FileMode.Open,FileAccess.Read);
							byte[] data1 = new byte[fs1.Length];
							byte[] data2 = new byte[fs2.Length];
							fs1.Read(data1,0,System.Convert.ToInt32(fs1.Length)); 
							fs2.Read(data2,0,System.Convert.ToInt32(fs2.Length)); 
							fs1.Close();
							fs2.Close();
							
							Console.Error.WriteLine("use assembly for(2) "+filename);
							Timingutil.info("reuse : "+filename);
//							loAssembly = Assembly.LoadFile(moduleName);
							//load all modules in lib


							loAssembly = Assembly.Load(data1,data2);

							string [] fileEntries = Directory.GetFiles(Directory.GetParent(filename).ToString()+"/libs/");
							foreach(string fileName2 in fileEntries)
							{
								DateTime filemtime2 = File.GetLastWriteTime(fileName2);
								string moduleName2 = directory+GetMD5(fileName2)+"_"+Commonutil.ConvertToUnixTimestamp(filemtime2).ToString();
									FileStream fs21 = new FileStream(moduleName2+".dll", FileMode.Open,FileAccess.Read);
									FileStream fs22 = new FileStream(moduleName2+".dll.mdb", FileMode.Open,FileAccess.Read);
									byte[] data21 = new byte[fs21.Length];
									byte[] data22 = new byte[fs22.Length];
									fs21.Read(data21,0,System.Convert.ToInt32(fs21.Length)); 
									fs22.Read(data22,0,System.Convert.ToInt32(fs22.Length)); 
									fs21.Close();
									fs22.Close();
									
									
									Console.Error.WriteLine("load sub assembly "+fileName2);
									Assembly loAssembly2 = Assembly.Load(data21,data22);
							}	
//							loAssembly = Assembly.Load(data1);
							//http://msdn.microsoft.com/en-us/library/system.reflection.moduleresolveeventhandler(VS.71).aspx
						}
						else
						{
							Timingutil.info("deleting old");
							File.Delete(fileEntry);
							File.Delete(fileEntry+".mdb");
						}
					}
				}	




				if(loAssembly == null)
				{
					if(codefileCache.Contains(filename))
						codefileCache.Remove(filename);

					string moduleName = "/tmp/www-data-temp-aspnet-0/"+GetMD5(filename)+"_"+Commonutil.ConvertToUnixTimestamp(filemtime).ToString()+".dll";
		
					Console.Error.WriteLine("compile "+filename);

					CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
					//ICodeCompiler loCompiler = new CSharpCodeProvider().CreateCompiler();
					CompilerParameters loParameters = new CompilerParameters();

					string [] fileEntries = Directory.GetFiles(Directory.GetParent(filename).ToString()+"/libs/");
					foreach(string fileName in fileEntries)
					{
						string pathtolib = CompileLib(fileName);
						loParameters.ReferencedAssemblies.Add(pathtolib);
					}	
					string [] fileEntries2 = Directory.GetFiles("/var/www_mono/bin/");
					foreach(string fileName in fileEntries2)
					{
						if(fileName.EndsWith(".dll"))
							loParameters.ReferencedAssemblies.Add(fileName);
					}	


					foreach (Assembly assem in AppDomain.CurrentDomain.GetAssemblies())
		    	    {
						//Console.Error.WriteLine(assem.Location);
						Match m = Regex.Match(assem.Location,@"^(/tmp/www-data-temp-aspnet-0/[0-9a-f]+/App_Code\.[0-9a-f]+\.dll)$");
						loParameters.ReferencedAssemblies.Add(m.Groups[1].Value);
						//Console.Error.WriteLine("Ma"+m.Groups[1].Value);
					}
					loParameters.ReferencedAssemblies.Add("System.dll");
					loParameters.ReferencedAssemblies.Add("Npgsql.dll");
					loParameters.ReferencedAssemblies.Add("System.Web.dll");
					loParameters.ReferencedAssemblies.Add("System.Drawing.dll");
					loParameters.ReferencedAssemblies.Add("System.Web.Services.dll");
					loParameters.ReferencedAssemblies.Add("System.Data.dll");
					loParameters.GenerateInMemory = false;
					loParameters.IncludeDebugInformation = true;
					loParameters.CompilerOptions = "-debug";
					//cp.TempFiles = new TempFileCollection(".", true);

					loParameters.OutputAssembly = moduleName;


					Timingutil.start("compile "+filename);
//					CompilerResults loCompiled = loCompiler.CompileAssemblyFromFile(loParameters,filename);
					CompilerResults loCompiled = provider.CompileAssemblyFromFile(loParameters,filename);
					Timingutil.stop();

			
					if (loCompiled.Errors.HasErrors)    
					{
						string lcErrorMsg = "";
						lcErrorMsg = loCompiled.Errors.Count.ToString() + " Errors:";
						for (int x=0;x<loCompiled.Errors.Count;x++) 
							lcErrorMsg = lcErrorMsg  + "\r\nLine: " +
							loCompiled.Errors[x].Line.ToString() + " - " +
							loCompiled.Errors[x].ErrorText;
					
					
						throw new CompileException("Errors compiling '"+filename+"':\n\n"+lcErrorMsg);
					}
					loAssembly = loCompiled.CompiledAssembly;
				}
			
				Timingutil.start("createInstance");
			
				object loObject  = loAssembly.CreateInstance("Page");
				
				CacheObject cacheObject = new CacheObject(loObject,filemtime);
				
				
				codefileCache.Add(filename,cacheObject);
				Timingutil.stop();
				
				
		}

		public bool IsReusable 
		{
			get { return false; }
		}
		
		public static string GetMD5(string input)
		{
			byte[] hashdata = System.Text.Encoding.ASCII.GetBytes(input);
			hashdata = Md5Provider2.ComputeHash(hashdata);
			string output = "";
			for (int i=0; i < hashdata.Length; i++) output += hashdata[i].ToString("x2").ToLower();
			return output;
		}

	}
	
	class CompileException : Exception
	{ 
		public string s;
		public CompileException():base()
		{
			s=null;
		}
		public CompileException(string message):base()
		{
			s=message.ToString();
		}
		public CompileException(string message,Exception myNew):base(message,myNew)
		{
			s=message.ToString();// Stores new exception message into class member s
		}
	}

}

