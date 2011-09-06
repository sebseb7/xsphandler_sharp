using System;
//using System.Collections;
using System.Threading;
using System.Collections.Generic;


// use TLS instead of threadInfoTable: Thread.AllocateNamedDataSlot("sleeptime");
// oops: some Members are not capitalized (hmm... coded to much perl in between) --> fit that

namespace Net.Exse.Timingutil
{
	public class ThreadInfo
	{
		public Node root;
		public Node currentNode;
	}


	public class Node
	{
		public List<Node> childs;
		public ulong stop;
		public ulong start;
		public string nodetype,key;
		public Object parentNode;

		public Node(string p1,string p2)
		{
			childs = new List<Node>();
			stop = 0;
			start = 0;
			parentNode = null;
			key = p1;
			nodetype = p2;
			parentNode = null;
		}
	}
	

	public sealed class Timingutil
	{
		private static ulong GetUnixTime()
		{
			return (ulong) ((DateTime.UtcNow.Ticks - 621355968000000000L ) / 10L);
		}

		private static Dictionary<int,ThreadInfo> threadInfoTable = new Dictionary<int,ThreadInfo>();
//		private static SynchronizedKeyedCollection<int,ThreadInfo> threadInfoTable = new SynchronizedKeyedCollection<int,ThreadInfo>();
		private static Object threadInfoTableLock = new Object();
		
	
		public static void start(string name)
		{
			lock(threadInfoTableLock)
			{
				ThreadInfo threadInfo = (ThreadInfo)threadInfoTable[Thread.CurrentThread.GetHashCode()];

				Node newNode = new Node(name,"timer");
				newNode.parentNode = threadInfo.currentNode;
				newNode.start = GetUnixTime();
				threadInfo.currentNode.childs.Add(newNode);
				threadInfo.currentNode = newNode;
			}
			//Console.Error.WriteLine("TM start:"+name);
		}

		public static void stop()
		{
			lock(threadInfoTableLock)
			{
				ThreadInfo threadInfo = (ThreadInfo)threadInfoTable[Thread.CurrentThread.GetHashCode()];

				threadInfo.currentNode.stop = GetUnixTime();
				threadInfo.currentNode = (Node)threadInfo.currentNode.parentNode;
			}
			//Console.Error.WriteLine("TM stop");
		}

		public static void info(string name)
		{
			lock(threadInfoTableLock)
			{
				ThreadInfo threadInfo = (ThreadInfo)threadInfoTable[Thread.CurrentThread.GetHashCode()];

				Node newNode = new Node(name,"info");
				threadInfo.currentNode.childs.Add(newNode);
			}
		}

		public static void init()
		{
			ThreadInfo threadInfo = new ThreadInfo();
			
			lock(threadInfoTableLock)
			{
				if(threadInfoTable.ContainsKey(Thread.CurrentThread.GetHashCode()))
				{
					threadInfoTable.Remove(Thread.CurrentThread.GetHashCode());
				}

				threadInfoTable.Add(Thread.CurrentThread.GetHashCode(),threadInfo);
			}
			
			threadInfo.root = new Node("root","timer");
			threadInfo.root.start =  GetUnixTime();
			threadInfo.currentNode = threadInfo.root;
			
		}

		public static string dump()
		{
			string retval = "";
			lock(threadInfoTableLock)
			{
				ThreadInfo threadInfo = (ThreadInfo)threadInfoTable[Thread.CurrentThread.GetHashCode()];

				threadInfo.root.stop = GetUnixTime();
				Dumper myDumper = new Dumper();
				myDumper.dump(threadInfo.root,0,0);
				retval = myDumper.retval;
				threadInfoTable.Remove(Thread.CurrentThread.GetHashCode());
			}
			return retval;
		}
		public static Node objdump()
		{
			Node retval = null;
			lock(threadInfoTableLock)
			{
				ThreadInfo threadInfo = (ThreadInfo)threadInfoTable[Thread.CurrentThread.GetHashCode()];

				threadInfo.root.stop = GetUnixTime();
				retval = threadInfo.root;
				threadInfoTable.Remove(Thread.CurrentThread.GetHashCode());
			}
			return retval;
		}
	}
	class Dumper
	{
		public string retval = "";
		public void dump(Node node,int level,int levelmap)
		{

			{
				string prefix = "";
				int locallevelmap = levelmap;
				for (int i = level; i > 0; i--)
				{
					if (locallevelmap >= Math.Pow(i,2))
					{
						locallevelmap -= (int)Math.Pow(i,2);
						if (i == level)
						{
							prefix = "   |-" + prefix;
						}
						else
						{
							prefix = "   | " + prefix;
						}
					}
					else
					{
						if (i == level)
						{
							prefix = "   \\-" + prefix;
						}
						else
						{
							prefix = "     " + prefix;
						}
					}
				}
				retval += prefix + node.key + (node.nodetype=="timer" ?" (" +  (node.stop-node.start) + ")":"") + " \n";

			}

			for (int i = 0; i < node.childs.Count; i++)
			{
				int locallevelmap = levelmap;
				
				if(i < node.childs.Count-1)
				{
					locallevelmap += (int)Math.Pow((level+1),2);
				}
				dump((Node)node.childs[i],level+1,locallevelmap);
			}
			
		}
	}
}
