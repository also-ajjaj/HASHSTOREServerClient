using System.Net.Sockets;
using System.Text;

namespace Client;

class Program
{
    const string SERVER_IP = "127.0.0.1";
    const int SERVER_PORT = 9000;
    
    static TcpClient client;
    static NetworkStream stream;

    static void Main()
    {
        try
        {
            ConnectToServer();
            
            ShowMenu();
            
            CloseConnection();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    static void ConnectToServer()
    {
        client = new TcpClient();
        client.Connect(SERVER_IP, SERVER_PORT);
        stream = client.GetStream();
        Console.WriteLine($"Connected to {SERVER_IP}:{SERVER_PORT}\n");
    }
    
    static void CloseConnection()
    {
        if (stream != null) stream.Close();
        if (client != null) client.Close();
        Console.WriteLine("Connection closed");
    }
    
    static string ReadLine()
    {
        StringBuilder line = new StringBuilder();
        int byteData;

        while (true)
        {
            byteData = stream.ReadByte();
            if (byteData == -1) break;
            if (byteData == '\n') break;
            line.Append((char)byteData);
        }

        return line.ToString();
    }
    
    static byte[] ReadExactBytes(int length)
    {
        byte[] data = new byte[length];
        int bytesRead = 0;

        while (bytesRead < length)
        {
            int read = stream.Read(data, bytesRead, length - bytesRead);
            if (read == 0) break;
            bytesRead += read;
        }

        return data;
    }
    
    static void SendCommand(string command)
    {
        string fullCommand = command + "\n";
        byte[] data = Encoding.UTF8.GetBytes(fullCommand);
        
        stream.Write(data, 0, data.Length);
        stream.Flush(); 
    }
    
    static void SendBinaryData(byte[] data)
    {
        stream.Write(data, 0, data.Length);
        stream.Flush();
    }
    
    static void CommandList()
    {
        Console.WriteLine("=== LIST COMMAND ===");

        try
        {
            SendCommand("LIST");

            string header = ReadLine();
            Console.WriteLine($"Server: {header}\n");
            
            if (!header.StartsWith("200"))
            {
                Console.WriteLine("Error: Server returned error\n");
                return;
            }
            
            string[] parts = header.Split(' ');
            int fileCount = int.Parse(parts[2]);
            
            Console.WriteLine($"Found {fileCount} files:\n");
            Console.WriteLine("Hash | Description");
            Console.WriteLine("-----|------------");
            
            for (int i = 0; i < fileCount; i++)
            {
                string fileLine = ReadLine();
                
                string[] fileParts = fileLine.Split(new char[] { ' ' }, 2);
                string hash = fileParts[0];
                string description = fileParts.Length > 1 ? fileParts[1] : "";
                
                Console.WriteLine($"{hash} | {description}");
            }

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}\n");
        }
    }
    
    static void CommandGet(string hash)
    {
        Console.WriteLine($"=== GET COMMAND ===");
        Console.WriteLine($"Downloading hash: {hash.Substring(0, 16)}...\n");

        try
        {
            SendCommand($"GET {hash}");
            
            string header = ReadLine();
            Console.WriteLine($"Server: {header}");
            
            if (header.StartsWith("404"))
            {
                Console.WriteLine("Error: File not found\n");
                return;
            }

            if (!header.StartsWith("200"))
            {
                Console.WriteLine("Error: Server error\n");
                return;
            }
            
            string[] parts = header.Split(new char[] { ' ' }, 4);
            int length = int.Parse(parts[2]);
            string description = parts.Length > 3 ? parts[3] : "unknown";

            Console.WriteLine($"File size: {length} bytes");
            Console.WriteLine($"Description: {description}\n");
            
            byte[] fileData = ReadExactBytes(length);
            
            string filename = $"down_{description}";
            File.WriteAllBytes(filename, fileData);

            Console.WriteLine($"File saved as: {filename}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}\n");
        }
    }
    
    static void CommandUpload(string filename, string description)
    {
        Console.WriteLine($"=== UPLOAD COMMAND ===");
        Console.WriteLine($"Uploading file: {filename}");
        Console.WriteLine($"Description: {description}\n");

        try
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine($"Error: File '{filename}' not found\n");
                return;
            }

            byte[] fileData = File.ReadAllBytes(filename);
            int length = fileData.Length;
            
            SendCommand($"UPLOAD {length} {description}");
            
            SendBinaryData(fileData);
            
            string response = ReadLine();
            Console.WriteLine($"Server: {response}");
            
            if (response.StartsWith("200"))
            {
                string[] parts = response.Split(' ');
                string hash = parts[2];
                Console.WriteLine($"Upload successful!");
                Console.WriteLine($"File hash: {hash}\n");
            }
            else if (response.StartsWith("409"))
            {
                string[] parts = response.Split(' ');
                string hash = parts[2];
                Console.WriteLine($"File already exists with hash: {hash}\n");
            }
            else
            {
                Console.WriteLine("Error during upload\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}\n");
        }
    }
    
    static void CommandDelete(string hash)
    {
        Console.WriteLine($"=== DELETE COMMAND ===");
        Console.WriteLine($"Deleting hash: {hash.Substring(0, 16)}...\n");

        try
        {
            SendCommand($"DELETE {hash}");
            
            string response = ReadLine();
            Console.WriteLine($"Server: {response}");
            
            if (response.StartsWith("200"))
            {
                Console.WriteLine("File deleted successfully\n");
            }
            else if (response.StartsWith("404"))
            {
                Console.WriteLine("Error: File not found\n");
            }
            else
            {
                Console.WriteLine("Error during delete\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}\n");
        }
    }
    
    static void ShowMenu()
    {
        Console.WriteLine("=== HASHSTORE CLIENT ===\n");
        Console.WriteLine("Commands:");
        Console.WriteLine("  list                              - List all files");
        Console.WriteLine("  get <hash>                        - Download file");
        Console.WriteLine("  upload <filename> <description>   - Upload file");
        Console.WriteLine("  delete <hash>                     - Delete file");
        Console.WriteLine("  quit                              - Exit\n");

        while (true)
        {
            Console.Write("> ");
            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            string[] args = input.Split(' ');
            string command = args[0].ToLower();

            try
            {
                if (command == "list")
                {
                    CommandList();
                }
                else if (command == "get" && args.Length >= 2)
                {
                    CommandGet(args[1]);
                }
                else if (command == "upload" && args.Length >= 3)
                {
                    string filename = args[1];
                    string desc = string.Join(" ", args, 2, args.Length - 2);
                    CommandUpload(filename, desc);
                }
                else if (command == "delete" && args.Length >= 2)
                {
                    CommandDelete(args[1]);
                }
                else if (command == "quit")
                {
                    break;
                }
                else
                {
                    Console.WriteLine("Invalid command. Type 'list', 'get <hash>', 'upload <file> <desc>', 'delete <hash>', or 'quit'\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}\n");
            }
        }
    }
}