using System.ComponentModel;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Data;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;

namespace FreeWebApiSecurity.WebApi.BusinessObjects;

[DefaultProperty(nameof(UserName))]
public class ApplicationUser : PermissionPolicyUser, IObjectSpaceLink, ISecurityUserWithLoginInfo {
    public ApplicationUser() : base() {
        UserLogins = new List<ApplicationUserLoginInfo>();
    }

    [Browsable(false)]
    [DevExpress.ExpressApp.DC.Aggregated]
    public virtual IList<ApplicationUserLoginInfo> UserLogins { get; set; }
    IEnumerable<ISecurityUserLoginInfo> IOAuthSecurityUser.UserLogins => UserLogins.OfType<ISecurityUserLoginInfo>();

    ISecurityUserLoginInfo ISecurityUserWithLoginInfo.CreateUserLoginInfo(string loginProviderName, string providerUserKey) {
        ApplicationUserLoginInfo result = ((IObjectSpaceLink)this).ObjectSpace.CreateObject<ApplicationUserLoginInfo>();
        result.LoginProviderName = loginProviderName;
        result.ProviderUserKey = providerUserKey;
        result.User = this;
        return result;
    }
}

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

    public void OnLoaded()
    {
    }

    public void OnSaving()
    {
    }
}
