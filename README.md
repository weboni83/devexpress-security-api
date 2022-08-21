# Devexpress Security Web API 만들기





```
https://nuget.devexpress.com/6s9TKQ44aDhuo56h9uwFbftYOAndjf7jAiq2OorovKxlSrFMOM/api
```

[Download Your Products | DevExpress](https://www.devexpress.com/ClientCenter/DownloadManager/?utm_source=SecurityApi&utm_medium=Download&utm_campaign=FreeOffers&utm_content=FreeOffers_SecurityApi_Download)



[A 1-Click Solution for CRUD Web API with Role-based Access Control via EF Core & ASP.NET - YouTube](https://www.youtube.com/watch?v=T7y4gwc1n4w&list=PL8h4jt35t1wiM1IOux04-8DiofuMEB33G&index=2&ab_channel=DevExpress)



## 구현

1. DbSet 추가

> FreeWebApiSecurityEFCoreDbContext

```csharp
public DbSet<Post> Posts { get; set; }
```

2. BO 추가

> services.AddXafWebApi

```csharp
options.BusinessObject<Post>();
```

3. model 추가

> WebApi.BusinessObjects

```csharp
public class Post : IXafEntityObject, IObjectSpaceLink {
    [Key]
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public virtual ApplicationUser Author { get; set; }
    IObjectSpace IObjectSpaceLink.ObjectSpace { get; set; }

    public void OnCreated()
    {
        var objectSpace = ((IObjectSpaceLink)this).ObjectSpace;
        if (objectSpace.IsNewObject(this))
        {
            Author = objectSpace.FindObject<ApplicationUser>(CriteriaOperator.Parse("ID=CurrentUserId()"));
        }
    }
}
```

4. Updater 구현(DB에 사용자 권한 설정 )

```csharp
var editorUser = ObjectSpace.FirstOrDefault<ApplicationUser>(user => user.UserName == "Editor") ?? ObjectSpace.CreateObject<ApplicationUser>();
if (ObjectSpace.IsNewObject(editorUser))
{
    //create Editor User/Role
    editorUser.UserName = "Editor";
    editorUser.SetPassword("");

    var editorRole = ObjectSpace.CreateObject<PermissionPolicyRole>();
    editorRole.Name = "EditorRole";
    editorRole.AddTypePermission<Post>(SecurityOperations.CRUDAccess, SecurityPermissionState.Allow);
    editorRole.AddTypePermission<ApplicationUser>(SecurityOperations.CRUDAccess, SecurityPermissionState.Allow);

    editorUser.Roles.Add(editorRole); ;

    //Create Viewer User/Role
    var viewerUser = ObjectSpace.CreateObject<ApplicationUser>();
    viewerUser.UserName = "Viewer";
    viewerUser.SetPassword("");
    var viewerRole = ObjectSpace.CreateObject<PermissionPolicyRole>();
    viewerRole.Name = "ViewerRole";
    viewerRole.AddTypePermission<Post>(SecurityOperations.Read, SecurityPermissionState.Allow);
    viewerUser.Roles.Add(viewerRole);

    //commit
    ObjectSpace.CommitChanges();

    //assign authentication type
    foreach (var user in new[] {editorUser, viewerUser}.Cast<ISecurityUserWithLoginInfo>())
    {
        user.CreateUserLoginInfo(SecurityDefaults.PasswordAuthentication, ObjectSpace.GetKeyValueAsString(user));
    }
}
```

6. Update Model 실행



## 테스트

1. swagger 로 실행
