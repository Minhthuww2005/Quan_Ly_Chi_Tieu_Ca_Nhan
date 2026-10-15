using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WalletFunding.Api;

// Execute the production controller adapter with a recording repository; no database or provider.
var cases=new (string Method,string Path,bool Write)[]
{
    ("GET","/api/wallet",false),
    ("GET","/api/payments/orders",false),
    ("GET","/api/payments/vnpay/verify",false),
    ("GET","/api/payments/vnpay/verify/",false),
    ("GET","/api/payments/vnpay/ipn",true),
    ("GET","/api/payments/vnpay/ipn/",true),
    ("GET","/api/payments/vnpay/ipn///",true),
    ("GET","/api/payments//vnpay/ipn",true),
    ("GET","/API/payments/vnpay/ipn/",true),
    ("GET","/api/Payments/vnpay/ipn/",true),
    ("GET","/api/payments/VNPAY/IPN",true),
    ("GET","/api/payments/vnpay/ipn/extra",true),
    ("GET","/api/payments/vnpay/ipn-lookalike",false),
    ("POST","/api/wallet",true),
    ("PUT","/api/auth/profile",true),
    ("DELETE","/api/cards/1",true),
    ("HEAD","/api/payments/vnpay/ipn",true),
};
var results=new List<object>();
foreach(var item in cases)
{
    var repository=new RecordingRepository();
    var controller=new ProbeController(new StubService(),repository);
    controller.ControllerContext=new(){HttpContext=new DefaultHttpContext()};
    controller.Request.Method=item.Method;controller.Request.Path=item.Path;
    var response=await controller.Execute();
    if(repository.Write!=item.Write||!repository.Committed||response is not JsonResult{StatusCode:200})throw new Exception("Boundary failed: "+item);
    results.Add(new{name=item.Method+" "+item.Path+" selects "+(item.Write?"write transaction":"read connection"),status="passed"});
}
var output=Path.GetFullPath(args.FirstOrDefault()??"docs/report-revision/boundary-results.json");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{generatedAt=DateTimeOffset.UtcNow,passed=results.Count,scope="production WalletControllerBase with DefaultHttpContext and recording repository; no real HTTP, database, authentication or provider settlement",results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS: {results.Count} production controller transaction-boundary checks.");

sealed class ProbeController(IWalletService service,IWalletRepository repository):WalletControllerBase(service,repository)
{public Task<IActionResult> Execute()=>ExecuteRequest();}
sealed class StubService:IWalletService
{public Task<ServiceResponse> ExecuteAsync(WalletRequest request)=>Task.FromResult(new ServiceResponse(new JsonObject()));}
sealed class RecordingRepository:IWalletRepository
{
    public bool? Write{get;private set;}public bool Committed{get;private set;}
    public Task Begin(bool write){Write=write;return Task.CompletedTask;}
    public Task Commit(){Committed=true;return Task.CompletedTask;}
    public Task<List<JsonObject>> All(string table)=>Task.FromResult(new List<JsonObject>());
    public Task Add(string table,JsonObject data)=>Task.CompletedTask;
    public Task<int> Next(string table,string field)=>Task.FromResult(1);
    public Task<string> NextCode(string table,string prefix)=>Task.FromResult(prefix+"1");
    public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}
