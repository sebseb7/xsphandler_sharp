using System;
using System.Web;
using System.Web.SessionState;
using System.Collections;
using System.Xml;
using System.IO;

public class Page 
{ 	
	// static ?!?!?!?
	
	public static XmlDocument Query(HttpRequest req, HttpSessionState session,Hashtable xsltParameters)
	{
		XmlDocument inputDoc = new XmlDocument();
		
		
		inputDoc.AppendChild(inputDoc.CreateProcessingInstruction("http-redirect", "/"+xsltParameters["base"].ToString()+"/static.cs?page=index"));


		return inputDoc;


	} 

}
