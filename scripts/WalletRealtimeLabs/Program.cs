using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.IdentityModel.Tokens;
using RevenueManagement.Api.Infrastructure.Authorization;
using RevenueManagement.Api.Infrastructure.Realtime;
using RevenueManagement.Api.Infrastructure.Workers;

// Synthetic keys and group membership exist only in this isolated loopback host.
const string key="QA_ONLY_REALTIME_KEY_FOR_LOCAL_LAB_2026";
var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options=>
{
    options.MapInboundClaims=false;
    options.TokenValidationParameters=new(){ValidateIssuer=true,ValidIssuer="HTDPay.ResearchLab",ValidateAudience=true,ValidAudience="HTDPay.ResearchLab.Client",ValidateLifetime=true,ValidateIssuerSigningKey=true,IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))};
});
builder.Services.AddAuthorization(options=>options.AddPolicy(AuthorizationPolicies.GroupMember,policy=>policy.AddRequirements(new LabMembershipRequirement())));
builder.Services.AddSingleton<IAuthorizationHandler,LabMembershipHandler>();
builder.Services.AddRealtimeServices();
await using var app=builder.Build();app.UseAuthentication();app.UseAuthorization();app.MapHub<DashboardHub>("/lab/realtime");
await app.StartAsync();
string Token(string subject)=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("HTDPay.ResearchLab","HTDPay.ResearchLab.Client",[new Claim("sub",subject)],expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),SecurityAlgorithms.HmacSha256)));
HubConnection Client(string? subject)=>new HubConnectionBuilder().WithUrl(app.Urls.Single()+"/lab/realtime",options=>{options.Transports=HttpTransportType.WebSockets;if(subject is not null)options.AccessTokenProvider=()=>Task.FromResult<string?>(Token(subject));}).Build();
var results=new List<object>();
void Check(bool condition,string name)
{if(!condition)throw new InvalidOperationException("FAIL: "+name);results.Add(new{name,status="passed"});Console.WriteLine("PASS: "+name);}
async Task Until(Func<bool> predicate)
{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));while(!predicate())await Task.Delay(20,timeout.Token);}
await using var anonymous=Client(null);await using var a=Client("1");await using var b=Client("2");await using var outsider=Client("3");
int aEvents=0,bEvents=0,outsiderEvents=0;
using var aHandler=a.On<long,string,DateTime>("RevenueChanged",(_,_,_)=>Interlocked.Increment(ref aEvents));
using var bHandler=b.On<long,string,DateTime>("RevenueChanged",(_,_,_)=>Interlocked.Increment(ref bEvents));
using var outsiderHandler=outsider.On<long,string,DateTime>("RevenueChanged",(_,_,_)=>Interlocked.Increment(ref outsiderEvents));
try
{
    var denied=false;try{await anonymous.StartAsync();}catch(HttpRequestException e){denied=e.StatusCode==System.Net.HttpStatusCode.Unauthorized;}
    Check(denied,"Actual SignalR negotiate request rejects a client without a bearer token");
    await a.StartAsync();await b.StartAsync();await outsider.StartAsync();
    Check(a.State==HubConnectionState.Connected&&b.State==HubConnectionState.Connected,"Two authenticated .NET clients connect to the real loopback hub");
    Check(outsider.State==HubConnectionState.Connected,"Authenticated outsider can connect but still needs resource authorization");
    await a.InvokeAsync("JoinGroup",10L);await b.InvokeAsync("JoinGroup",20L);
    var joinDenied=false;try{await outsider.InvokeAsync("JoinGroup",10L);}catch(HubException){joinDenied=true;}
    Check(joinDenied,"Actual hub invocation refuses unauthorized group membership");
    var publisher=app.Services.GetRequiredService<IRevenueChangedPublisher>();
    await publisher.PublishAsync(10);await Until(()=>Volatile.Read(ref aEvents)==1);await Task.Delay(400);
    Check(aEvents==1,"Group 10 client receives the publisher event through actual SignalR transport");
    Check(bEvents==0,"Group 20 client does not receive the group 10 event within the observation window");
    Check(outsiderEvents==0,"Rejected outsider receives no group event within the observation window");
    var originalId=a.ConnectionId;await a.StopAsync();await a.StartAsync();
    Check(a.ConnectionId is not null&&a.ConnectionId!=originalId,"Stop and start create a new SignalR connection id");
    await publisher.PublishAsync(10);await Task.Delay(400);
    Check(aEvents==1,"A new connection does not inherit the previous connection group membership");
    await a.InvokeAsync("JoinGroup",10L);await publisher.PublishAsync(10);await Until(()=>Volatile.Read(ref aEvents)==2);
    Check(aEvents==2,"Explicit rejoin restores delivery after a new connection starts");
    await publisher.PublishAsync(20);await Until(()=>Volatile.Read(ref bEvents)==1);await Task.Delay(400);
    Check(aEvents==2&&bEvents==1,"Group 20 publication stays isolated from the rejoined group 10 client");
    Check(outsiderEvents==0,"Outsider receives no event after both groups publish");
    await a.StopAsync();await b.StopAsync();await outsider.StopAsync();
    Check(a.State==HubConnectionState.Disconnected&&b.State==HubConnectionState.Disconnected&&outsider.State==HubConnectionState.Disconnected,"All authenticated test connections stop cleanly");
    var output=Path.GetFullPath(args.FirstOrDefault()??Path.Combine(AppContext.BaseDirectory,"../../../../../docs/report-revision/realtime-results.json"));
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{generatedAt=DateTimeOffset.UtcNow,passed=results.Count,events=new{group10=aEvents,group20=bEvents,outsider=outsiderEvents},scope="existing research hub and publisher on real Kestrel loopback with .NET SignalR WebSocket clients; synthetic JWT and membership handler; no database, browser, network loss or HTD PAY integration",results},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"SUMMARY: {results.Count} realtime checks passed; events [{aEvents},{bEvents},{outsiderEvents}].");
}
finally{await app.StopAsync();}

sealed class LabMembershipRequirement:IAuthorizationRequirement;
sealed class LabMembershipHandler:AuthorizationHandler<LabMembershipRequirement,long>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,LabMembershipRequirement requirement,long resource)
    {var subject=context.User.FindFirst("sub")?.Value;if(subject=="1"&&resource==10||subject=="2"&&resource==20)context.Succeed(requirement);return Task.CompletedTask;}
}
