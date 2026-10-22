// Environment provisioning/ownership verification only. All scenario assertions use Playwright.
using System.Text.Json;
using System.Security.Cryptography;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
var root = Environment.GetEnvironmentVariable("UC17_RUNTIME") ?? throw new Exception("Missing runtime");
var manifestPath = Path.Combine(root, "ownership.json");
var manifest = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(manifestPath))!;
var run = manifest["runId"];
var name = "UC17_Scenario_" + run;
if (manifest["database"] != name || !Guid.TryParseExact(run, "N", out _) || manifest["root"] != root) throw new Exception("Ownership invalid");
var cs = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = name, IntegratedSecurity = true, TrustServerCertificate = true };
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(cs.ConnectionString).Options);
if (args[0] == "cleanup") {
  await using var conn = new SqlConnection(cs.ConnectionString); await conn.OpenAsync();
  var cmd = conn.CreateCommand(); cmd.CommandText = "SELECT CAST(value AS nvarchar(128)) FROM sys.extended_properties WHERE class=0 AND name='UC17_RunId'";
  if ((string?)await cmd.ExecuteScalarAsync() != run) throw new Exception("STOP: ownership mismatch");
  await conn.CloseAsync(); SqlConnection.ClearAllPools();
  await db.Database.EnsureDeletedAsync();
  cs.InitialCatalog = "master"; await using var master = new SqlConnection(cs.ConnectionString); await master.OpenAsync();
  var verify = master.CreateCommand(); verify.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@n"; verify.Parameters.AddWithValue("@n", name);
  if ((int)(await verify.ExecuteScalarAsync())! != 0) throw new Exception("Cleanup not verified");
  Console.WriteLine("CLEANUP_VERIFIED: database absent"); return;
}
if (args[0] == "audit") {
  var audits = await db.AuditLogs.Select(a => new { a.Action, a.EntityName, a.EntityId }).ToListAsync();
  var payments = await db.Payments.Select(p => new { p.PaymentId, p.ContractId, p.Amount, p.RequestId }).ToListAsync();
  File.WriteAllText(Path.Combine(root,"audit.json"), JsonSerializer.Serialize(new { audits, payments })); return;
}
if(args[0]=="booking") {
 await using var conn=new SqlConnection(cs.ConnectionString); await conn.OpenAsync(); var guard=conn.CreateCommand(); guard.CommandText="SELECT CAST(value AS nvarchar(128)) FROM sys.extended_properties WHERE class=0 AND name='UC17_RunId'";
 if((string?)await guard.ExecuteScalarAsync()!=run) throw new Exception("Ownership mismatch");
 var b=new WeddingBooking { BookingCode="DT-CREATE",GuestCount=10,EstimatedTotal=1000,StatusId=802,Customer=new Customer {CustomerCode="KH-CREATE",FullName="Synthetic create",PhoneNumber="0900000000",DataStatusId=1},HallSchedule=new HallSchedule {Date=DateTime.Today.AddDays(30),Shift="Ca trưa",StatusId=301,DataStatusId=1,Hall=new Hall {HallCode="H-CREATE",HallName="Synthetic",MaximumCapacity=100,RentalPrice=1000,StatusId=201,DataStatusId=1}}};
 db.WeddingBookings.Add(b); await db.SaveChangesAsync(); Console.WriteLine("CREATE_FIXTURE_READY");return;
}
if (!await db.Database.EnsureCreatedAsync()) throw new Exception("Refuse existing database");
await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_addextendedproperty @name=N'UC17_RunId', @value={run}");
await db.Database.OpenConnectionAsync();
Console.WriteLine($"TARGET_VERIFIED: {db.Database.GetDbConnection().DataSource}/{db.Database.GetDbConnection().Database}");
await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(Environment.CurrentDirectory,"Backend/HeThongDatTiecCuoi_API/Sql/Updates/20261021_UC17Payments.sql")));
db.DataStatuses.Add(new() { DataStatusId=1, DataStatusCode="EXISTING", DataStatusName="Synthetic" });
var statuses = new[] { (101,"ACCOUNT","ACTIVE"),(201,"HALL","AVAILABLE"),(301,"HALL_SCHEDULE","AVAILABLE"),(802,"BOOKING","CONFIRMED"),(901,"CONTRACT","DRAFT"),(902,"CONTRACT","ACTIVE"),(903,"CONTRACT","COMPLETED"),(904,"CONTRACT","CANCELLED"),(1001,"PAYMENT","PENDING"),(1002,"PAYMENT","SUCCESS"),(1003,"PAYMENT","FAILED"),(1004,"PAYMENT","CANCELLED") };
foreach(var (id,g,c) in statuses) db.Statuses.Add(new() { StatusId=id,StatusGroup=g,StatusCode=c,StatusName=c });
var roles = new[] { RoleNames.Admin,RoleNames.Manager,RoleNames.HallManager,RoleNames.Coordinator,RoleNames.Customer };
var finalRun = Environment.GetEnvironmentVariable("UC17_FINAL") == "1";
var password = "Uc17!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(16))+"a";
for(byte i=1;i<=5;i++) {
  if(finalRun && i is not (2 or 5)) continue;
 var user = new User { Email=$"role{i}@uc17.invalid",Role=new Role { RoleId=i, RoleName=roles[i-1] },StatusId=101,DataStatusId=1 };
 user.PasswordHash=new PasswordHasher<User>().HashPassword(user,password);
 if(i>=2&&i<=4) user.Employee=new Employee { EmployeeCode=$"E{i}",FullName=$"Synthetic role{i}",PhoneNumber="0900000000",DataStatusId=1 };
 if(i==5) user.Customer=new Customer { CustomerCode="USER5",FullName="Synthetic customer",PhoneNumber="0900000000",DataStatusId=1 };
 db.Users.Add(user);
}
await db.SaveChangesAsync();
var ids = new Dictionary<string,int>();
foreach(var key in new[]{"S01","S02","S03","S03bad","S04D","S04A","S04race","S05","S06","S06closed","S06cancel","S07","S07bad","S08","S08other","S09same","S09over","S10","S11","S12","S13","S14","MCP"}) {
  if(finalRun && key is not ("S02" or "S04A" or "S10" or "S12" or "S14" or "MCP")) continue;
 var c=new Contract { ContractCode="HD-"+key,TotalValue=1000,RequiredDepositAmount=key.StartsWith("S04")?null:300,ContractContent=key=="S03bad"?"":"Synthetic contract",PaymentTerms="Synthetic terms",StatusId=key=="S06closed"?903:key=="S06cancel"?904:key is "S04A" or "S04race" or "S05" or "S07" or "S07bad" or "S08" or "S08other" or "S09same" or "S09over" or "S11"?902:901,
 Booking=new WeddingBooking { BookingCode="DT-"+key,GuestCount=10,StatusId=802,Customer=new Customer { CustomerCode="KH-"+key,FullName="Synthetic "+key,PhoneNumber="0900000000",DataStatusId=1 },HallSchedule=new HallSchedule { Date=DateTime.Today.AddDays(30),Shift="Ca trưa",StatusId=301,DataStatusId=1,Hall=new Hall { HallCode="H-"+key,HallName="Synthetic hall",MaximumCapacity=100,RentalPrice=1000,StatusId=201,DataStatusId=1 } } } };
 db.Contracts.Add(c); await db.SaveChangesAsync(); ids[key]=c.ContractId;
  if(finalRun && key=="S14") {
    var dates=new[]{new DateTime(2026,1,9,23,59,59),new DateTime(2026,1,10),new DateTime(2026,1,11,23,59,59),new DateTime(2026,1,12)};
    var codes=new[]{"DATE-BEFORE","DATE-START","DATE-END","DATE-AFTER"};
    for(var i=0;i<4;i++) db.Payments.Add(new() {ContractId=c.ContractId,Amount=10,PaymentType="Đặt cọc",PaymentMethod="Synthetic",StatusId=1002,PaymentDate=dates[i],TransactionCode=codes[i]});
  }
  else if(key is "S05" or "S14") foreach(var s in new[]{1001,1002,1003,1004}) db.Payments.Add(new() { ContractId=c.ContractId,Amount=s==1002?100.25m:50,PaymentType="Đặt cọc",PaymentMethod="Synthetic",StatusId=s,PaymentDate=DateTime.Now });
 if(key=="S07bad") db.Payments.Add(new() { ContractId=c.ContractId,Amount=1001,PaymentType="Đặt cọc",StatusId=1002,PaymentDate=DateTime.Now });
}
await db.SaveChangesAsync();
if (Environment.GetEnvironmentVariable("UC17_REVIEW") == "1") {
  // Probe only the random database just created by this process; never app config.
  var legacyId = ids["S04A"];
  db.Payments.Add(new() { ContractId=legacyId, Amount=5, PaymentType="Đặt cọc", StatusId=1002 });
  await db.SaveChangesAsync();
  await db.Database.ExecuteSqlRawAsync("DROP INDEX UX_ThanhToan_RequestId ON ThanhToan; ALTER TABLE ThanhToan DROP COLUMN RequestId; ALTER TABLE HopDong DROP COLUMN SoTienCocCamKet;");
  var upgrade = await File.ReadAllTextAsync(Path.Combine(Environment.CurrentDirectory,"Backend/HeThongDatTiecCuoi_API/Sql/Updates/20261021_UC17Payments.sql"));
  var hostile = "SET ANSI_NULLS OFF; SET ANSI_PADDING OFF; SET ANSI_WARNINGS OFF; SET ARITHABORT OFF; SET CONCAT_NULL_YIELDS_NULL OFF; SET QUOTED_IDENTIFIER OFF; SET NUMERIC_ROUNDABORT ON; ";
  await db.Database.ExecuteSqlRawAsync(hostile + upgrade);
  await db.Database.ExecuteSqlRawAsync(hostile + upgrade);
  db.ChangeTracker.Clear();
  var legacy = await db.Contracts.FindAsync(legacyId);
  var payment = await db.Payments.SingleAsync(p=>p.ContractId==legacyId);
  using var probe = db.Database.GetDbConnection().CreateCommand();
  probe.CommandText="SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ThanhToan') AND name=N'UX_ThanhToan_RequestId' AND is_unique=1 AND has_filter=1 AND is_disabled=0";
  var indexed = (int)(await probe.ExecuteScalarAsync())! == 1;
  File.WriteAllText(Path.Combine(root,"review-sql.json"),JsonSerializer.Serialize(new { runs=2, uniqueFilteredIndex=indexed, legacyCommitmentNull=legacy!.RequiredDepositAmount==null, legacyRequestNull=payment.RequestId==null, legacyAmount=payment.Amount }));
  foreach (var contract in await db.Contracts.Where(c=>c.ContractId!=legacyId).ToListAsync()) contract.RequiredDepositAmount=300;
  await db.SaveChangesAsync();
}
File.WriteAllText(Path.Combine(root,"fixtures.json"),JsonSerializer.Serialize(new{ids,password}));
File.WriteAllText(Path.Combine(root,"connection.txt"),cs.ConnectionString);
if(finalRun) {
 var customer=await db.Users.Include(u=>u.Customer).Include(u=>u.Role).SingleAsync(u=>u.Email=="role5@uc17.invalid");
 File.WriteAllText(Path.Combine(root,"customer-fixture.json"),JsonSerializer.Serialize(new {role=customer.Role.RoleName,customer.StatusId,customer.DataStatusId,hasCustomerProfile=customer.Customer!=null,passwordVerified=new PasswordHasher<User>().VerifyHashedPassword(customer,customer.PasswordHash,password)!=PasswordVerificationResult.Failed}));
}
Console.WriteLine("SCHEMA_AND_SYNTHETIC_FIXTURES_READY");
