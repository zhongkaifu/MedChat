using AdvUtils;
using Seq2SeqSharp._SentencePiece;
using Seq2SeqSharp.Utils;
using TensorSharp.CUDA.ContextState;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using System.Net;

namespace MedChat
{
    /// <summary>
    /// 
    /// </summary>
    internal static class Extensions
    {
        public static T ToEnum<T>(this string s) where T : struct => Enum.Parse<T>(s, true);
        public static int ToInt(this string s) => int.Parse(s);
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Logger.Initialize(Logger.Destination.Console | Logger.Destination.Logfile, Logger.Level.err | Logger.Level.warn | Logger.Level.info | Logger.Level.debug, $"{nameof(MedChat)}_{Seq2SeqSharp.Utils.Utils.GetTimeStamp(DateTime.Now)}.log");
            Logger.WriteLine($"LetsChat.Health written by Zhongkai Fu(fuzhongkai@gmail.com)");

            var Configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

            // Initialize translator
            var translationAPIKey = Configuration["Translator:Key"];
            var translationAPILocation = Configuration["Translator:Location"];

            Translator.Initialize(translationAPIKey, translationAPILocation);

            Settings.nBest = int.Parse(Configuration["nBestResult"]);
            Settings.Language = Configuration["Language"];
            Settings.SupportNoteDraft = bool.Parse(Configuration["SupportNoteDraft"]);
            Settings.BlobLogs = new MedChat.BlobLogs(Configuration["BlobLogging:ConnectionString"].ToString(), Configuration["BlobLogging:ContainerName"].ToString());

            // Decide which chat model should be loaded.
            Settings.ChatModel = ChatModel.InHouse;

            if (String.IsNullOrEmpty(Configuration["OpenAIKey"]) == false)
            {
                OpenAI.OpenAIKeyString = Configuration["OpenAIKey"];
            }

            if (String.IsNullOrEmpty(Configuration["ChatModel"]) == false)
            {
                if (Configuration["ChatModel"].Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
                {
                    Settings.ChatModel = ChatModel.OpenAI;

                    Logger.WriteLine($"We switch nBest from '{Settings.nBest}' to 1 because of using OpenAI model.");
                    Settings.nBest = 1;
                }
            }
            Logger.WriteLine($"Chat model type: '{Settings.ChatModel}'");

            // Load hotfix file
            if (String.IsNullOrEmpty(Configuration["HotfixDictionary"]) == false)
            {
                string hotfixFilePath = Configuration["HotfixDictionary"];
                if (File.Exists(hotfixFilePath))
                {
                    Logger.WriteLine($"Loading hotfix dictionary from '{hotfixFilePath}'");
                    Settings.Hotfix = new HashSet<string>();
                    foreach (var line in File.ReadAllLines(hotfixFilePath))
                    {
                        Settings.Hotfix.Add(line);
                    }
                }
                else
                {
                    Logger.WriteLine($"Hotfix file '{hotfixFilePath}' doesn't exist.");
                }
            }

            if (String.IsNullOrEmpty(Configuration["Seq2Seq:ModelFilePath"]) == false)
            {
                Logger.WriteLine($"Loading Seq2Seq model '{Configuration["Seq2Seq:ModelFilePath"]}'");

                var modelFilePath = Configuration["Seq2Seq:ModelFilePath"];
                var maxTestSrcSentLength = String.IsNullOrEmpty(Configuration["Seq2Seq:MaxSrcTokenSize"]) ? 1024 : int.Parse(Configuration["Seq2Seq:MaxSrcTokenSize"]);
                var maxTestTgtSentLength = String.IsNullOrEmpty(Configuration["Seq2Seq:MaxTgtTokenSize"]) ? 1024 : int.Parse(Configuration["Seq2Seq:MaxTgtTokenSize"]);
                var maxTokenToGeneration = String.IsNullOrEmpty(Configuration["Seq2Seq:MaxTokenToGeneration"]) ? 8192 : int.Parse(Configuration["Seq2Seq:MaxTokenToGeneration"]);
                var processorType = String.IsNullOrEmpty(Configuration["Seq2Seq:ProcessorType"]) ? ProcessorTypeEnums.CPU : (Configuration["Seq2Seq:ProcessorType"].ToEnum<ProcessorTypeEnums>());
                var deviceIds = String.IsNullOrEmpty(Configuration["Seq2Seq:DeviceIds"]) ? "0" : Configuration["Seq2Seq:DeviceIds"];
                var decodingStrategyEnum = String.IsNullOrEmpty(Configuration["Seq2Seq:TokenGenerationStrategy"]) ? DecodingStrategyEnums.Sampling : Configuration["Seq2Seq:TokenGenerationStrategy"].ToEnum<DecodingStrategyEnums>();
                var gpuMemoryUsageRatio = String.IsNullOrEmpty(Configuration["Seq2Seq:GPUMemoryUsageRatio"]) ? 0.99f : float.Parse(Configuration["Seq2Seq:GPUMemoryUsageRatio"]);
                var mklInstructions = String.IsNullOrEmpty(Configuration["Seq2Seq:MKLInstructions"]) ? "" : Configuration["Seq2Seq:MKLInstructions"];
                var beamSearchSize = String.IsNullOrEmpty(Configuration["Seq2Seq:BeamSearchSize"]) ? 1 : int.Parse(Configuration["Seq2Seq:BeamSearchSize"]);
                var blockedTokens = String.IsNullOrEmpty(Configuration["Seq2Seq:BlockedTokens"]) ? "" : Configuration["Seq2Seq:BlockedTokens"];
                var modelType = String.IsNullOrEmpty(Configuration["Seq2Seq:ModelType"]) ? ModelType.EncoderDecoder : Configuration["Seq2Seq:ModelType"].ToEnum<ModelType>();
                var wordMappingFilePath = Configuration["Seq2Seq:WordMappingFilePath"];
                var enableTensorCore = string.IsNullOrEmpty(Configuration["Seq2Seq:EnableTensorCore"]) ? true : bool.Parse(Configuration["Seq2Seq:EnableTensorCore"]);
                var compilerOptions = Configuration["Seq2Seq:CompilerOptions"];
                var amp = String.IsNullOrEmpty(Configuration["Seq2Seq:AMP"]) ? false : bool.Parse(Configuration["Seq2Seq:AMP"]);
                var cudaMemoryAllocatorType = String.IsNullOrEmpty(Configuration["Seq2Seq:CudaMemoryAllocatorType"]) ? CudaMemoryDeviceAllocatorType.CudaMemoryPool : Configuration["Seq2Seq:CudaMemoryAllocatorType"].ToEnum<CudaMemoryDeviceAllocatorType>();
                var penaltyScore = String.IsNullOrEmpty(Configuration["Seq2Seq:PenaltyScore"]) ? 2.0f : float.Parse(Configuration["Seq2Seq:PenaltyScore"]);
                Settings.PenaltyScore = penaltyScore;

                SentencePiece? srcSpm = null;
                if (String.IsNullOrEmpty(Configuration["SourceSpm:ModelFilePath"]) == false)
                {
                    srcSpm = new SentencePiece(Configuration["SourceSpm:ModelFilePath"]);
                }

                SentencePiece? tgtSpm = null;
                if (String.IsNullOrEmpty(Configuration["TargetSpm:ModelFilePath"]) == false)
                {
                    tgtSpm = new SentencePiece(Configuration["TargetSpm:ModelFilePath"]);
                }

                Seq2SeqInstance.Initialization(modelFilePath,
                                               maxTestSrcSentLength,
                                               maxTestTgtSentLength,
                                               maxTokenToGeneration,
                                               processorType,
                                               deviceIds,
                                               srcSpm,
                                               tgtSpm,
                                               decodingStrategyEnum,
                                               memoryUsageRatio: gpuMemoryUsageRatio,
                                               mklInstructions: mklInstructions,
                                               beamSearchSize: beamSearchSize,
                                               blockedTokens: blockedTokens,
                                               modelType: modelType,
                                               wordMappingFilePath: wordMappingFilePath,
                                               enableTensorCore: enableTensorCore,
                                               compilerOptions: compilerOptions,
                                               amp: amp,
                                               cudaMemoryDeviceAllocatorType: cudaMemoryAllocatorType);
            }

            var builder = WebApplication.CreateBuilder(args);
            if (String.IsNullOrEmpty(Configuration["AzureAd"]) == false)
            {
                Logger.WriteLine($"Load AzureAd settings...");

                builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"));

                //Add services to the container.
               builder.Services.AddControllersWithViews(options =>
               {
                   var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                   options.Filters.Add(new AuthorizeFilter(policy));

               });
            }
            else
            {
                builder.Services.AddControllersWithViews();
            }

            builder.Services.AddDistributedMemoryCache();

            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromDays(1);
                options.Cookie.HttpOnly= true;
                options.Cookie.Name = "LetsChatHealth.Session";
                options.Cookie.IsEssential= true;
            });

            var app = builder.Build();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
            });

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseSession();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}