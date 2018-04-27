using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WebAppShutdown
{
    public class ControlShutdown : IStartupFilter, IDisposable
    {
        private readonly ILogger<ControlShutdown> logger;
        private readonly Thread pingThread;
        private readonly AutoResetEvent pingUnblock = new AutoResetEvent(initialState: false);
        private readonly Thread streamThread;
        private volatile bool stopping;
        private volatile bool disposed;

        public ControlShutdown(IApplicationLifetime applicationLifetime, ILogger<ControlShutdown> logger)
        {
            this.logger = logger;
            this.pingThread = new Thread(PingThread);
            this.streamThread = new Thread(StreamThread);
            applicationLifetime.ApplicationStarted.Register(OnApplicationStarted);
            applicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
            applicationLifetime.ApplicationStopped.Register(OnApplicationStopped);
        }

        private void PingThread(object obj)
        {
            using (logger.BeginScope("PingThread"))
            {
                while (!disposed)
                {
                    try
                    {
                        StatusCheck();
                        pingUnblock.WaitOne(TimeSpan.FromSeconds(2));
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning("Caught {Error}", ex.Message);
                    }
                }
            }
        }

        private void StreamThread(object obj)
        {
            using (logger.BeginScope("StreamThread"))
            {
                logger.LogInformation("Begin streaming request");
                try
                {
                    using (var client = new HttpClient())
                    {
                        var result = client.GetAsync("http://localhost:29145/stream", HttpCompletionOption.ResponseHeadersRead)
                            .ConfigureAwait(false).GetAwaiter().GetResult();

                        result.Content.CopyToAsync(new LoggingStream(logger)).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Caught {Error}", ex.Message);
                }
                finally
                {
                    logger.LogInformation("End streaming request");
                }
            }
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                logger.LogInformation("Configure called");

                builder.Map("/$status", map => map.Run(async ctx =>
                {
                    ctx.Response.StatusCode = stopping ? 503 : 200;
                }));

                builder.Map("/stream", map => map.Run(async ctx =>
                {
                    using (logger.BeginScope("StreamRequest"))
                    {
                        logger.LogInformation("Begin streaming response");
                        try
                        {
                            ctx.Features.Get<IHttpBufferingFeature>()?.DisableResponseBuffering();
                            while (true)
                            {
                                logger.LogInformation("Sending {Count} bytes", 4);
                                await ctx.Response.Body.WriteAsync(new byte[] { 65, 66, 67, 68 }, 0, 4, ctx.RequestAborted);
                                await Task.Delay(TimeSpan.FromSeconds(5));
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning("Caught {Error}", ex.Message);
                        }
                        finally
                        {
                            logger.LogInformation("End streaming response");
                        }
                    }
                }));

                next(builder);
            };
        }

        public void OnApplicationStarted()
        {
            pingThread.Start();
            streamThread.Start();
            using (logger.BeginScope("OnApplicationStarted"))
            {
                StallCallback(3, TimeSpan.FromSeconds(.5));
            }
        }

        public void OnApplicationStopping()
        {
            using (logger.BeginScope("OnApplicationStopping"))
            {
                stopping = true;

                // 8 seconds of 503 to fail away load balancers
                StallCallback(4, TimeSpan.FromSeconds(2));
            }
        }

        public void OnApplicationStopped()
        {
            using (logger.BeginScope("OnApplicationStopped"))
            {
                StallCallback(3, TimeSpan.FromSeconds(.5));
            }
        }

        public void Dispose()
        {
            logger.LogInformation("Dispose called");
            disposed = true;
            this.pingThread.Join();
            this.streamThread.Join();
            logger.LogInformation("Background thread ended");
        }

        private void StallCallback(int count, TimeSpan delay)
        {
            try
            {
                logger.LogInformation("============================================================================");
                logger.LogInformation("Begin Callback");

                for(var index = 0; index < count; index++)
                {
                    pingUnblock.Set();
                    StatusCheck();
                    Thread.Sleep(delay);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Caught {Error}", ex.Message);
            }
            finally
            {
                logger.LogInformation("End Callback");
                logger.LogInformation("----------------------------------------------------------------------------");
            }
        }

        private void StatusCheck()
        {
            var result1 = new HttpClient().GetAsync("http://localhost:29145/$status").ConfigureAwait(false).GetAwaiter().GetResult();
            logger.LogInformation("/$status -> {status}", result1.StatusCode);

            var result2 = new HttpClient().GetAsync("http://localhost:29145/request").ConfigureAwait(false).GetAwaiter().GetResult();
            logger.LogInformation("/request -> {status}", result2.StatusCode);
        }

    }
}
