using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Sitecore.Diagnostics;
using Sitecore.Salesforce.Api;
using Sitecore.Salesforce.Client;
using Sitecore.Salesforce.Client.Data;
using Sitecore.Salesforce.Configuration;
using Sitecore.Salesforce.Data;
using Sitecore.Salesforce.Soql;

namespace Sitecore.Salesforce.CampaignExtention.Api
{
  public class RolesApi : IRolesApi //Sitecore.Salesforce.Api.IRolesApi
  {
    public string ContactObjectName { get; set; }

    public string RoleAssociationObjectName { get; set; }

    public string RoleObjectName { get; set; }

    public string RoleFieldName { get; set; }

    protected readonly ISalesforceFieldMapping FieldMapping;

    protected readonly ISalesforceClient Client;

    public RolesApi(ISalesforceClient client, ISalesforceFieldMapping fieldMapping)
    {
      Assert.ArgumentNotNull(client, "client");

      this.Client = client;
      this.FieldMapping = fieldMapping;
    }

    public virtual List<SalesforceRole> GetAllRoles()
    {
      var query = string.Format("SELECT Id, {0} FROM {1}", this.RoleFieldName, this.RoleObjectName);

      return new List<SalesforceRole>(this.Client.QueryAll<SalesforceRole>(query));
    }

    public virtual List<SalesforceRole> GetRolesForUser(string loginName)
    {
      Assert.ArgumentNotNullOrEmpty(loginName, "loginName");

      var query = string.Format(
          "SELECT Id, {0} FROM {1} WHERE Id IN (SELECT CampaignId FROM {2} WHERE {3}.{4} {5})",
        this.RoleFieldName,
        this.RoleObjectName,
        this.RoleAssociationObjectName,
        this.ContactObjectName,
        this.FieldMapping.Login,
        ComparisonOperatorParser.Parse(ComparisonOperator.Equals, loginName));

      return this.Client.QueryAll<SalesforceRole>(query).ToList();
    }

    public virtual SalesforceRole Get(string roleName)
    {
      Assert.ArgumentNotNullOrEmpty(roleName, "roleName");

      var query = string.Format("SELECT Id, {0} FROM {1} WHERE {0} {2}"
          ,this.RoleFieldName
          ,this.RoleObjectName
          ,ComparisonOperatorParser.Parse(ComparisonOperator.Equals, roleName));

      return this.Client.QueryAll<SalesforceRole>(query).FirstOrDefault();
    }

    public virtual List<string> GetUsersInRole(string roleName)
    {
      Assert.ArgumentNotNullOrEmpty(roleName, "roleName");
      
      var query = string.Format(
        "SELECT {1}.{0} FROM {1} WHERE Id IN (SELECT ContactId FROM {2} WHERE {3}.{4} {5})",
        this.FieldMapping.Login,
        this.ContactObjectName,
        this.RoleAssociationObjectName,
        this.RoleObjectName,
        this.RoleFieldName,
        ComparisonOperatorParser.Parse(ComparisonOperator.Equals, roleName));

      return this.GetUsersByQuery(query);
    }

    public virtual List<string> FindUsersInRole(string roleName, string usernameToMatch)
    {
      Assert.ArgumentNotNullOrEmpty(roleName, "roleName");
      Assert.ArgumentNotNullOrEmpty(usernameToMatch, "usernameToMatch");

      var query = string.Format("SELECT {1} FROM {2} WHERE {0}.{1} like '%{5}%' and {3}.{6} = '{4}'",
      this.ContactObjectName,
      this.FieldMapping.Login,
      this.RoleAssociationObjectName,
      this.RoleObjectName,
           roleName,
           usernameToMatch,
      this.RoleFieldName);

      return this.GetUsersByQuery(query);
    }

    public virtual OperationResult Create(string roleName)
    {
      Assert.ArgumentNotNullOrEmpty(roleName, "roleName");

      var result = this.Client.HttpPost<OperationResult>(
        string.Format("sobjects/{0}", this.RoleObjectName),
        new Dictionary<string, string> { { "Name", roleName }, { "IsActive", "true" } });

      return result;
    }

    public virtual bool DeleteById(string roleId)
    {
      Assert.ArgumentNotNullOrEmpty(roleId, "roleId");

      return this.Client.HttpDelete(string.Format("sobjects/{0}/{1}", this.RoleObjectName, roleId));
    }

    public virtual OperationResult AddUserToRole(string roleId, string userId)
    {
      Assert.ArgumentNotNullOrEmpty(roleId, "roleId");
      Assert.ArgumentNotNullOrEmpty(userId, "userId");

      var data = new Dictionary<string, string>()
              {
                { string.Format("{0}Id", this.RoleObjectName), roleId },
                { string.Format("{0}Id", this.ContactObjectName), userId },
                { "Status", "Sent" }
              };
      
      return this.Client.HttpPost<OperationResult>(string.Format("sobjects/{0}", this.RoleAssociationObjectName), data);
    }

    public virtual bool DeleteUserFromRole(string roleName, string loginName)
    {
      Assert.ArgumentNotNullOrEmpty(roleName, "roleName");
      Assert.ArgumentNotNullOrEmpty(loginName, "loginName");

      var query = string.Format("SELECT Id FROM {2} WHERE {0}.{1} {3} and {4}.{5} {6}",
      this.ContactObjectName,
      this.FieldMapping.Login,
      this.RoleAssociationObjectName,
      ComparisonOperatorParser.Parse(ComparisonOperator.Equals, loginName),
      this.RoleObjectName,
      this.RoleFieldName,
      ComparisonOperatorParser.Parse(ComparisonOperator.Equals, roleName)
      );

      var roleAssociation = this.Client.QueryAll<RoleAssociation>(query).FirstOrDefault();

      if (roleAssociation != null)
      {
        return this.Client.HttpDelete(string.Format("sobjects/{0}/{1}", this.RoleAssociationObjectName, roleAssociation.Id));
      }

      return false;
    }

    protected virtual List<string> GetUsersByQuery(string query)
    {
      Assert.ArgumentNotNullOrEmpty(query, "query");

      var result = this.Client.QueryAll<JToken>(query);

      ISet<string> users = new HashSet<string>();

      foreach (JToken token in result)
      {
        users.Add(token.Value<string>(this.FieldMapping.Login));
      }
      return new List<string>(users);
    }
  }
}
