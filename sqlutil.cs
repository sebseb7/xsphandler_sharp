namespace Net.Exse.Sqlutil
{
	using Net.Exse.Timingutil;
	using Net.Exse.Commonutil;
	using System;
	using System.Collections;
	using System.Data;
	using Npgsql;
	using System.Text.RegularExpressions;


	public sealed class Sqlutil : IDisposable
	{

		private NpgsqlConnection dbcon = null;
		private NpgsqlTransaction transaction = null;
		
		public Sqlutil(string connectionString)
		{
			Timingutil.start("Create DB Conn");
			dbcon = new NpgsqlConnection(connectionString);
			dbcon.Open();
			Timingutil.stop();
		}
		public void Reopen()
		{
			dbcon.Open();
		}
		
		public void Dispose()
		{
			Timingutil.start("close DB Conn");
			dbcon.Close();
			dbcon  = null;
			//fix that !!
			//GC.SupresFinialize(this);
			Timingutil.stop();
		}

		public void Begin()
		{
			transaction = dbcon.BeginTransaction();
		}
		public void Commit()
		{
			transaction.Commit();
			transaction = null;
		}
		public void Rollback()
		{
			transaction.Rollback();
			transaction = null;
		}

		public int Query(string sql)
		{
			Timingutil.start("DB Query:"+sql);
			NpgsqlCommand cmd = new NpgsqlCommand(sql,dbcon);
			int rows = cmd.ExecuteNonQuery();
			Timingutil.stop();
			return rows;
		}
		public int QueryP(string sql, byte[] bytes)
		{
		
		
			Timingutil.start("DB Query:"+sql);

			NpgsqlCommand cmd = new NpgsqlCommand(sql, dbcon);
			NpgsqlParameter param = new NpgsqlParameter(":bytesData", DbType.Binary);
			param.Value = bytes;

			cmd.Parameters.Add(param);
			int rows = cmd.ExecuteNonQuery();
			Timingutil.stop();
			return rows;
		}


		public Hashtable QueryRowHash(string sql)
		{
			Timingutil.start("DB QueryRow hash:"+sql);
			NpgsqlCommand cmd = new NpgsqlCommand(sql,dbcon);
			NpgsqlDataReader dr = cmd.ExecuteReader();
			if (dr.HasRows)
			{
				dr.Read();

				Hashtable result = new Hashtable();
				
				for(int i = 0 ; i < dr.FieldCount; i++)
				{
					result.Add(dr.GetName(i),Convert(dr[i]));
				}

				dr.Close();
				Timingutil.stop();
				return result;
			}
			else
			{
				Timingutil.stop();
				return null;
			}
		
		}

		public int Query(string sql, params object [] args)
		{
			sql = Render(sql,args);
			return Query(sql);
		}
		public object[] QueryRow(string sql, params object [] args)
		{
			sql = Render(sql,args);
			return QueryRow(sql);
		}
		public object QueryRowSingle(string sql, params object [] args)
		{
			sql = Render(sql,args);
			return QueryRowSingle(sql);
		}
		public object[] QueryRows(string sql, params object [] args)
		{
			sql = Render(sql,args);
			return QueryRows(sql);
		}
		public object[] QueryRowsHash(string sql, params object [] args)
		{
			sql = Render(sql,args);
			return QueryRowsHash(sql);
		}
		public Hashtable QueryRowHash(string sql, params object [] args)
		{
			sql = Render(sql,args);
			return QueryRowHash(sql);
		}


		public Object[] QueryRow(string sql)
		{
			Timingutil.start("DB QueryRow arr:"+sql);
			NpgsqlCommand cmd = new NpgsqlCommand(sql,dbcon);
			NpgsqlDataReader dr = cmd.ExecuteReader();
			if (dr.HasRows)
			{
				dr.Read();

				object[] result = new object[dr.FieldCount];
				
				int i;
				for( i = 0 ; i < dr.FieldCount; i++)
				{
					result[i] = Convert(dr[i]);
				}

				Timingutil.info("X-Rows:"+dr.FieldCount.ToString());
				Timingutil.info("Rows:"+i.ToString());

				dr.Close();
				Timingutil.stop();
				return result;
			}
			else
			{
				Timingutil.stop();
				return null;
			}
		
		}

		public Object QueryRowSingle(string sql)
		{
			Timingutil.start("DB QueryRow arr:"+sql);
			NpgsqlCommand cmd = new NpgsqlCommand(sql,dbcon);
			NpgsqlDataReader dr = cmd.ExecuteReader();
			if (dr.HasRows)
			{
				dr.Read();

				object result = Convert(dr[0]);

				dr.Close();
				Timingutil.stop();
				return result;
			}
			else
			{
				Timingutil.stop();
				return null;
			}
		
		}


		public Object[] QueryRows(string sql)
		{
			Timingutil.start("DB QueryRows arr:"+sql);
			NpgsqlCommand cmd = new NpgsqlCommand(sql,dbcon);
			NpgsqlDataReader dr = cmd.ExecuteReader();
			int currentRow = 0;
			if (dr.HasRows)
			{
				ArrayList result = new ArrayList();

				while(dr.Read())
				{
					object[] row = new object[dr.FieldCount];
				
					for(int i = 0 ; i < dr.FieldCount; i++)
					{
						row[i] = dr[i];
					}
					result.Add(row);
					currentRow++;
				}

				dr.Close();
				Timingutil.stop();
				return result.ToArray();
			}
			else
			{
				Timingutil.stop();
				return null;
			}
		
		}

		public Object[] QueryRowsHash(string sql)
		{
			Timingutil.start("DB QueryRows hash:"+sql);
			NpgsqlCommand cmd = new NpgsqlCommand(sql,dbcon);
			NpgsqlDataReader dr = cmd.ExecuteReader();
			int currentRow = 0;
			if (dr.HasRows)
			{
				ArrayList result = new ArrayList();
				
				while(dr.Read())
				{
					Hashtable row = new Hashtable();
				
					for(int i = 0 ; i < dr.FieldCount; i++)
					{
						row.Add(dr.GetName(i),dr[i]);
					}
					result.Add(row);
					currentRow++;
				}

				dr.Close();
				Timingutil.stop();
				return result.ToArray();
			}
			else
			{
				Timingutil.stop();
				return null;
			}
		
		}
		
		private static Object Convert(Object cell)
		{
			Object retval = null;
			
			string type = cell.GetType().ToString();

			if(type == "System.DBNull")
			{
				retval = null;
			}
			else
			{
				retval = cell;
			}
		
			return retval;
		}
		
		private static string Render(string sql, params object [] args)
		{
			{
				Regex regex = new Regex(@"(?'g1'\$\$(?'g2'\d+)i)");

				foreach(Match match in regex.Matches(sql) )
				{
					string match1 =match.Groups["g1"].Captures[0].Value;
					int index = Int32.Parse(match.Groups["g2"].Captures[0].Value);

					int number = (int)args[index-1];
					sql = sql.Replace(match1,number.ToString());
				}
			}
			{
				Regex regex = new Regex(@"(?'g1'\$\$(?'g2'\d+)c)");

				foreach(Match match in regex.Matches(sql) )
				{
					string match1 =match.Groups["g1"].Captures[0].Value;
					int index = Int32.Parse(match.Groups["g2"].Captures[0].Value);

					string chars = (string)args[index-1];
					chars = chars.Replace("'","\\'");
					
					sql = sql.Replace(match1,"'"+chars+"'");
				}
			}
			return sql;
		}
		
	}

}

