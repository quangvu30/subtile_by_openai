using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

class Program
{
    private static readonly string EncryptionKey = "veryhighsecurity";
    static string ffmpegPath = Path.Combine(Environment.CurrentDirectory, "ffmpeg\\bin\\ffmpeg.exe");
    static void Main(string[] args)
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string apiKey = "";
        while (true)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("=============== Menu ===============");
            Console.WriteLine("1. Set OpenAI key");
            Console.WriteLine("2. Select MP3/MP4 file");
            Console.WriteLine("3. Exit");

            Console.Write("Enter your choice: ");
            int choice = 0;
            try
            {
                choice = Convert.ToInt32(Console.ReadLine());
            }
            catch (Exception e)
            {
                Console.WriteLine("Invalid choice. Please try again.");
                continue;
            }
            switch (choice)
            {
                case 1:
                    Console.Write("Enter your OpenAI API key: ");
                    apiKey = Console.ReadLine();
                    Console.Write("Do you want to save key (y/n): ");
                    string saveKey = Console.ReadLine();
                    if (saveKey == "y")
                    {
                        string encryptedKey = Encrypt(apiKey);
                        File.WriteAllText("key", encryptedKey);
                    }
                    Console.WriteLine("API key set to: " + apiKey);
                    break;
                case 2:
                    apiKey = File.Exists("key") ? Decrypt(File.ReadAllText("key")) : "";
                    if (apiKey == "")
                    {
                        Console.WriteLine("Please set the API key first.");
                        break;
                    }
                    Console.Write("Enter mp3/mp4 file: ");
                    string file = Console.ReadLine();
                    
                    if (File.Exists(file))
                    {
                        string extension = Path.GetExtension(file);
                        switch (extension)
                        {
                            case ".mp3":
                                break;
                            case ".mp4":
                                splitAudio(file);
                                optimizeAudio();
                              
                                string transcript = GetTranscriptFromOpenAI(apiKey, "temp.ogg").Result;
                                if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, "output")) == false)
                                {
                                    Directory.CreateDirectory("output");
                                }
                                string output = Path.Combine([Environment.CurrentDirectory, "output" , Path.GetFileNameWithoutExtension(file) + ".srt"]);
                                File.WriteAllText(output, transcript);
                                Console.WriteLine("Transcript saved to " + output);
                                File.Delete("temp.mp3");
                                File.Delete("temp.ogg");
                                break;
                            default:
                                Console.WriteLine("Invalid file type. Please try again.");
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine("File does not exist.");
                    }
                    break;
                case 3:
                    Console.WriteLine("Exiting...");
                    return;
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    break;
            }
        }
    }

    static void splitAudio(string file)
    {
        Console.WriteLine("Splitting audio...");
        if (File.Exists("temp.mp3"))
        {
            File.Delete("temp.mp3");
        }
        string command = ffmpegPath + " -i \"" + file + "\" -vn -acodec libmp3lame temp.mp3";
        runCommand(command);
    }

    static void optimizeAudio()
    {
        Console.WriteLine("Optimizing audio...");
        if (File.Exists("temp.ogg"))
        {
            File.Delete("temp.ogg");
        }
        string command = ffmpegPath + " -i temp.mp3  -vn -map_metadata -1 -ac 1 -c:a libopus -b:a 12k -application voip temp.ogg";
        runCommand(command);
    }

    static async Task<string> GetTranscriptFromOpenAI(string apiKey, string audioFilePath)
    {
        string apiEndpoint = "https://api.openai.com/v1/audio/transcriptions";

        using (var client = new HttpClient())
        {
            try
            {
                // Prepare the audio file
                var audioContent = new ByteArrayContent(File.ReadAllBytes(audioFilePath));
                audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/ogg");

                // Set up the request
                var request = new HttpRequestMessage(HttpMethod.Post, apiEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new MultipartFormDataContent
                {
                    { audioContent, "file", "temp.ogg" },
                    { new StringContent("whisper-1"), "model" },
                    { new StringContent("srt"), "response_format" }
                };

                // Send the request and get the response
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();

                // Parse the response JSON
                //var jsonResponse = JObject.Parse(responseString);
                //var transcription = jsonResponse.ToString();
                return responseString;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }
        }
    }

    public static string Encrypt(string plainText)
    {
        byte[] iv = new byte[16];
        byte[] array;

        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(EncryptionKey);
            aes.IV = iv;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                    {
                        streamWriter.Write(plainText);
                    }

                    array = memoryStream.ToArray();
                }
            }
        }

        return Convert.ToBase64String(array);
    }


    public static string Decrypt(string cipherText)
    {
        byte[] iv = new byte[16];
        byte[] buffer = Convert.FromBase64String(cipherText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(EncryptionKey);
            aes.IV = iv;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using (MemoryStream memoryStream = new MemoryStream(buffer))
            {
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader streamReader = new StreamReader(cryptoStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }
    }

    static void runCommand(string command)
    {
        // Create a new process.
        Process process = new Process();

        // Configure the process using the StartInfo properties.
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.Arguments = $"/c {command}"; // '/c' tells cmd to execute the command and exit.
        process.StartInfo.UseShellExecute = false; // Do not use the system shell to start the process.
        process.StartInfo.RedirectStandardOutput = true; // Redirect the standard output of the command. 
        process.StartInfo.CreateNoWindow = true; // Do not create a window for this process.

        // Start the process.
        process.Start();

        // Read the output of the command.
        string result = process.StandardOutput.ReadToEnd();

        // Wait for the process to finish.
        process.WaitForExit();

        // Display the command output.
        Console.WriteLine(result);
    }
}
