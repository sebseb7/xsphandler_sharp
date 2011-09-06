namespace Net.Exse.Cryptutil
{

	// make encrypt liste top not prepend random bytes, but a 4 byte IV vector (0 padded)
	// make enctyptlite and encrypt tp share code
	// make computehash urlsafe (base64 stripping)

	using System;
	using System.IO;
	using System.Text;
	using System.Security.Cryptography;

	//using Net.Exse.Timingutil;
	
	public sealed class Cryptutil
	{
	

		public static string Encrypt(string plain,string passPhrase)
		{
			HashAlgorithm hash = new SHA256Managed();
			byte[] keyBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(passPhrase));

			RijndaelManaged symmetricKey = new RijndaelManaged();

			byte[] plainBytes1 = Encoding.UTF8.GetBytes(plain);
			byte[] checksum = hash.ComputeHash(plainBytes1);
			byte[] padding = Encoding.UTF8.GetBytes("XXXXXXXXXXXXXXX");

			int addedBytes = 16 - (plainBytes1.Length % 16);
			if(addedBytes == 16) addedBytes =0;
			
			byte[] plainBytes = new byte[plainBytes1.Length + 32 + addedBytes];
			Array.Copy(plainBytes1,0,plainBytes,0,plainBytes1.Length);
			Array.Copy(checksum,0,plainBytes,plainBytes1.Length,32);
			Array.Copy(padding,0,plainBytes,plainBytes1.Length+32,addedBytes);


			symmetricKey.Mode = CipherMode.CFB;
			symmetricKey.BlockSize = 128;
			symmetricKey.GenerateIV();
			symmetricKey.Padding = PaddingMode.None;
			
			byte[] IV = symmetricKey.IV;

			symmetricKey.Key = keyBytes;

			ICryptoTransform encryptor = symmetricKey.CreateEncryptor();
			
			
		
		
			MemoryStream memoryStream = new MemoryStream();
			CryptoStream cryptoStream = new CryptoStream(memoryStream,encryptor,CryptoStreamMode.Write);
		
		
			
			cryptoStream.Write( plainBytes,0,plainBytes.Length);
			cryptoStream.FlushFinalBlock();
			
			byte[] cipherTextBytes = memoryStream.ToArray();
			memoryStream.Close();
			cryptoStream.Close();
			
			
			
			byte[] final = new Byte[ (cipherTextBytes.Length-addedBytes) + IV.Length ];
			Array.Copy( IV, 0, final, 0, IV.Length );
			Array.Copy( cipherTextBytes, 0, final, IV.Length, cipherTextBytes.Length-addedBytes );
			
			string cipheredText = Convert.ToBase64String(final);
			
			cipheredText = cipheredText.Replace("=","");
			cipheredText = cipheredText.Replace("A","AA");
			cipheredText = cipheredText.Replace("C","CC");
			cipheredText = cipheredText.Replace("+","BAB");
			cipheredText = cipheredText.Replace("/","BCB");
			
			return cipheredText;
			
		}

		public static string EncryptLite(string plain,string passPhrase)
		{
			HashAlgorithm hash = new SHA256Managed();
			byte[] keyBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(passPhrase));
			byte[] IVBytes = new byte[16];
			Array.Copy(keyBytes,0,IVBytes,0,16);
			
			RijndaelManaged symmetricKey = new RijndaelManaged();

			byte[] plainBytes1 = Encoding.UTF8.GetBytes(plain);
			byte[] checksum = hash.ComputeHash(plainBytes1);
			byte[] padding = Encoding.UTF8.GetBytes("XXXXXXXXXXXXXXX");

			int addedBytes = 16 - ((plainBytes1.Length+4) % 16);
			if(addedBytes == 16) addedBytes =0;
			
			byte[] plainBytes = new byte[plainBytes1.Length  + 4 + addedBytes];
			Array.Copy(plainBytes1,0,plainBytes,0,plainBytes1.Length);
			Array.Copy(checksum,0,plainBytes,plainBytes1.Length,4);
			Array.Copy(padding,0,plainBytes,plainBytes1.Length+4,addedBytes);


			symmetricKey.Mode = CipherMode.CFB;
			symmetricKey.BlockSize = 128;
			symmetricKey.Padding = PaddingMode.None;
			

			symmetricKey.Key = keyBytes;
			symmetricKey.IV = IVBytes;

			ICryptoTransform encryptor = symmetricKey.CreateEncryptor();
			
			
		
		
			MemoryStream memoryStream = new MemoryStream();
			CryptoStream cryptoStream = new CryptoStream(memoryStream,encryptor,CryptoStreamMode.Write);
		
		
			
			cryptoStream.Write( plainBytes,0,plainBytes.Length);
			cryptoStream.FlushFinalBlock();
			
			byte[] cipherTextBytes = memoryStream.ToArray();
			memoryStream.Close();
			cryptoStream.Close();
			
			
			
			byte[] final = new Byte[ (cipherTextBytes.Length-addedBytes) ];
			Array.Copy( cipherTextBytes, 0, final, 0, cipherTextBytes.Length-addedBytes );
			
			string cipheredText = Convert.ToBase64String(final);
			
			cipheredText = cipheredText.Replace("=","");
			cipheredText = cipheredText.Replace("A","AA");
			cipheredText = cipheredText.Replace("C","CC");
			cipheredText = cipheredText.Replace("+","BAB");
			cipheredText = cipheredText.Replace("/","BCB");
			
			return cipheredText;
			
		}
	

		public static string Decrypt(string ciphered,string passPhrase)
		{
			HashAlgorithm hash = new SHA256Managed();
			byte[] keyBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(passPhrase));

			ciphered = ciphered.Replace("BAB","+");
			ciphered = ciphered.Replace("BCB","/");
			ciphered = ciphered.Replace("AA","A");
			ciphered = ciphered.Replace("CC","C");

			for (int i=0; i < (ciphered.Length %4); i++) ciphered = ciphered+"=";

			byte[] allBytes = Convert.FromBase64String(ciphered);
			
			byte[] ivBytes = new Byte[16];

			int addedBytes = 16 - ((allBytes.Length-16) % 16);
			if(addedBytes == 16) addedBytes =0;


			byte[] cipheredBytes = new Byte[(allBytes.Length - 16)+addedBytes];

			
			Array.Copy( allBytes, 0, ivBytes, 0, ivBytes.Length );
			Array.Copy( allBytes, ivBytes.Length, cipheredBytes, 0, allBytes.Length - 16 );

			for (int i=0; i < addedBytes; i++) cipheredBytes[(allBytes.Length-16)+i]=(byte)'a';
			
			RijndaelManaged symmetricKey = new RijndaelManaged();
			symmetricKey.Mode = CipherMode.CFB;
			symmetricKey.IV = ivBytes;
			symmetricKey.Key = keyBytes;
			symmetricKey.Padding = PaddingMode.None;
			ICryptoTransform decryptor = symmetricKey.CreateDecryptor();
			
			
			MemoryStream memoryStream = new MemoryStream(cipheredBytes);
			CryptoStream cryptoStream = new CryptoStream(memoryStream,decryptor,CryptoStreamMode.Read);
		
			
			byte[] decryptedBytes = new byte[cipheredBytes.Length - addedBytes];
			int decryptedByteCount  = cryptoStream.Read(decryptedBytes,0,decryptedBytes.Length);
			memoryStream.Close();
			cryptoStream.Close();

		
			byte[] plainBytes = new byte[decryptedByteCount-32];	
			byte[] checksum2 = new byte[32];
			
			Array.Copy(decryptedBytes, 0, plainBytes, 0, cipheredBytes.Length - (addedBytes + 32));
			Array.Copy(decryptedBytes, cipheredBytes.Length - (addedBytes + 32), checksum2, 0, 32);

			string plain = Encoding.UTF8.GetString(plainBytes);
			
			byte[] checksum = hash.ComputeHash(plainBytes);

			for (int i=0; i < 32; i++)
			{
				if (checksum[i] != checksum2[i])
				{
					throw new Exception("decrypt checksum error");
				//	Console.Error.WriteLine("checksum error");
				//	Console.Error.WriteLine(checksum[i].ToString());
				//	Console.Error.WriteLine(checksum2[i].ToString());
				}
			}
			
			
	
			return plain;
		}

		public static string DecryptLite(string ciphered,string passPhrase)
		{
			HashAlgorithm hash = new SHA256Managed();
			byte[] keyBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(passPhrase));
			byte[] IVBytes = new byte[16];
			Array.Copy(keyBytes,0,IVBytes,0,16);

			ciphered = ciphered.Replace("BAB","+");
			ciphered = ciphered.Replace("BCB","/");
			ciphered = ciphered.Replace("AA","A");
			ciphered = ciphered.Replace("CC","C");

			for (int i=0; i < (ciphered.Length %4); i++) ciphered = ciphered+"=";

			byte[] allBytes = Convert.FromBase64String(ciphered);
			
			int addedBytes = 16 - ((allBytes.Length-16) % 16);
			if(addedBytes == 16) addedBytes =0;

			byte[] cipheredBytes = new Byte[allBytes.Length+addedBytes];
			
			Array.Copy(allBytes, 0, cipheredBytes, 0, allBytes.Length);

			for (int i=0; i < addedBytes; i++) cipheredBytes[allBytes.Length+i]=(byte)'a';
			
			RijndaelManaged symmetricKey = new RijndaelManaged();
			symmetricKey.Mode = CipherMode.CFB;
			symmetricKey.IV = IVBytes;
			symmetricKey.Key = keyBytes;
			symmetricKey.Padding = PaddingMode.None;
			ICryptoTransform decryptor = symmetricKey.CreateDecryptor();
			
			
			MemoryStream memoryStream = new MemoryStream(cipheredBytes);
			CryptoStream cryptoStream = new CryptoStream(memoryStream,decryptor,CryptoStreamMode.Read);
		
			
			byte[] decryptedBytes = new byte[cipheredBytes.Length - addedBytes];
			int decryptedByteCount  = cryptoStream.Read(decryptedBytes,0,decryptedBytes.Length);
			memoryStream.Close();
			cryptoStream.Close();

			//string plain2 = Encoding.UTF8.GetString(decryptedBytes);
			//Console.Error.WriteLine(addedBytes.ToString()+plain2);
		
			byte[] plainBytes = new byte[decryptedByteCount-(4)];	
			byte[] checksum2 = new byte[4];
			
			Array.Copy(decryptedBytes, 0, plainBytes, 0, cipheredBytes.Length - (addedBytes  + 4));
			Array.Copy(decryptedBytes, cipheredBytes.Length - (addedBytes + 4  ), checksum2, 0, 4);

			string plain = Encoding.UTF8.GetString(plainBytes);
			
			byte[] checksum = hash.ComputeHash(plainBytes);

			for (int i=0; i < 4; i++)
			{
				if (checksum[i] != checksum2[i])
				{
					throw new Exception("decrypt checksum error");
				//	Console.Error.WriteLine("checksum error");
				//	Console.Error.WriteLine(checksum[i].ToString());
				//	Console.Error.WriteLine(checksum2[i].ToString());
				}
			}
			
			
	
			return plain;
		}

	
		public static string ComputeHashWithSalt(string plain)
		{

			byte[] randomBytes = new byte[4];
			RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
			rng.GetBytes(randomBytes);
			int seed = ((randomBytes[0] & 0x7f) << 24) |(randomBytes[1] << 16) |(randomBytes[2] << 8 ) |(randomBytes[3]);

			Random  random = new Random(seed);
	        int saltSize = random.Next(4,8);
			byte[] saltBytes = new byte[saltSize];
			rng.GetNonZeroBytes(saltBytes);
			
			
			return ComputeHashWithSalt(plain,saltBytes);
		}

		public static string ComputeHash(string plain)
		{

			return ComputeHashWithSalt(plain,new byte[0]);
		}
		
		private static string ComputeHashWithSalt(string plain, byte[] saltBytes)
		{

			byte[] plainTextBytes = Encoding.UTF8.GetBytes(plain);
			byte[] plainTextWithSaltBytes = new byte[plainTextBytes.Length + saltBytes.Length];		
		
			for (int i=0; i < plainTextBytes.Length; i++) plainTextWithSaltBytes[i] = plainTextBytes[i];
			for (int i=0; i < saltBytes.Length; i++) plainTextWithSaltBytes[plainTextBytes.Length + i] = saltBytes[i];
		
			HashAlgorithm hash = new SHA256Managed();
		
			byte[] hashBytes = hash.ComputeHash(plainTextWithSaltBytes);
			byte[] hashWithSaltBytes = new byte[hashBytes.Length + saltBytes.Length];
			for (int i=0; i < hashBytes.Length; i++) hashWithSaltBytes[i] = hashBytes[i];
			for (int i=0; i < saltBytes.Length; i++) hashWithSaltBytes[hashBytes.Length + i] = saltBytes[i];
		
			return Convert.ToBase64String(hashWithSaltBytes);
		
		}
			
		public static bool VerifyHash(string plain, string hash)
		{	
			byte[] hashWithSaltBytes = Convert.FromBase64String(hash);
			{
				int hashSizeInBytes = 32;	
				if (hashWithSaltBytes.Length < hashSizeInBytes) return false;
			}

			return (hash == ComputeHashWithSalt(plain,GetSaltFromHash(hash)) );
		}

		private static byte[] GetSaltFromHash(string hash)
		{	
			byte[] hashWithSaltBytes = Convert.FromBase64String(hash);
			int hashSizeInBytes = 32;	
			
			byte[] saltBytes = new byte[hashWithSaltBytes.Length - hashSizeInBytes];
			for (int i=0; i < saltBytes.Length; i++) saltBytes[i] = hashWithSaltBytes[hashSizeInBytes + i];
			
			return saltBytes;
		}
			
	}
}
		
		
