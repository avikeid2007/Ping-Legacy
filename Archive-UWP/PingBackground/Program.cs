using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace PingBackground
{
    class Program
    {
        private static AppServiceConnection connection;
        public static bool IsPingStop { get; set; }
        public static string HostNameOrAddress { get; private set; }

        static async Task Main(string[] args)
        {
            await InitializeAppServiceConnectionAsync();
            await ThreadProcAsync();
        }

        static async Task ThreadProcAsync()
        {
            var pingSender = new Ping();
            while (!IsPingStop)
            {
                try
                {
                    if (!string.IsNullOrEmpty(HostNameOrAddress))
                    {
                        var reply = await pingSender.SendPingAsync(HostNameOrAddress);
                        if (reply?.Status == IPStatus.Success)
                        {
                            ValueSet request = new ValueSet();
                            request.Add("host", reply.Address.ToString());
                            request.Add("time", reply.RoundtripTime);
                            request.Add("size", reply.Buffer.Length);
                            request.Add("ttl", reply.Options?.Ttl);
                            request.Add("response", $"Reply from {reply.Address}: bytes={ reply.Buffer.Length} time={ reply.RoundtripTime}ms TTL={reply.Options?.Ttl}");
                            await connection.SendMessageAsync(request);
                        }
                        else
                        {
                            await UnsuccessReply(reply.Status);
                        }
                    }
                }
                catch
                {
                    await UnsuccessReply(IPStatus.DestinationUnreachable);
                }
                finally
                {
                    await Task.Delay(800);
                }
            }
        }

        private static async Task UnsuccessReply(IPStatus iPStatus)
        {
            ValueSet request = new ValueSet();
            request.Add("host", HostNameOrAddress);
            request.Add("time", -1000);
            request.Add("size", 0);
            request.Add("ttl", 0);
            request.Add("response", SetStatus(iPStatus));
            await connection.SendMessageAsync(request);
        }
        private static string SetStatus(IPStatus iPStatus)
        {
            switch (iPStatus)
            {
                case IPStatus.TimedOut:
                    return "Request timed out.";
                case IPStatus.DestinationNetworkUnreachable:
                    return "Destination Network Unreachable.";
                case IPStatus.DestinationHostUnreachable:
                    return "Destination Host Unreachable.";
                case IPStatus.DestinationProhibited:
                    return "Destination Prohibited.";

                case IPStatus.DestinationPortUnreachable:
                    return "Destination Port Unreachable";
                case IPStatus.NoResources:
                    return "No Resources";
                case IPStatus.BadOption:
                    return "Bad Option";
                case IPStatus.HardwareError:
                    return "Hardware Error";
                case IPStatus.PacketTooBig:
                    return "Packet Too Big";
                case IPStatus.BadRoute:
                    return "Bad Route";
                case IPStatus.TtlExpired:
                    return "Ttl Expired";
                case IPStatus.TtlReassemblyTimeExceeded:
                    return "Ttl Reassembly Time Exceeded";
                case IPStatus.ParameterProblem:
                    return "Parameter Problem";
                case IPStatus.SourceQuench:
                    return "Source Quench";
                case IPStatus.BadDestination:
                    return "Bad Destination ";
                case IPStatus.DestinationUnreachable:
                    return "Destination Unreachable";
                case IPStatus.TimeExceeded:
                    return "Time Exceeded";
                case IPStatus.BadHeader:
                    return "Bad Header";
                case IPStatus.UnrecognizedNextHeader:
                    return "Unrecognized Next Header";
                case IPStatus.IcmpError:
                    return "Icmp Error";
                case IPStatus.DestinationScopeMismatch:
                    return "Destination Scope Mismatch";
                default:
                    return "Request timed out.";
            }
        }
        private static async Task InitializeAppServiceConnectionAsync()
        {
            connection = new AppServiceConnection
            {
                AppServiceName = "PingInteropService",
                PackageFamilyName = Package.Current.Id.FamilyName
            };
            connection.RequestReceived += Connection_RequestReceived;
            connection.ServiceClosed += Connection_ServiceClosed;
            await connection.OpenAsync();
        }

        private static void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            Environment.Exit(0);
        }
        private static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            HostNameOrAddress = Convert.ToString(args.Request.Message["host"]);
            IsPingStop = Convert.ToBoolean(args.Request.Message["isStop"]);
        }
    }
}
