namespace CRMODataTests
{
	using CRMODataTests.Fixtures;
	using Newtonsoft.Json.Linq;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using Xunit;
	using Fixtures;

	/// <summary>
	/// Tests class <see cref="RetrieveTests"/>
	/// </summary>
	[Trait("Category", "Integration")]
	[Collection("HttpRequest")]
	public class RetrieveTests: IClassFixture<HttpRequestFixture>
	{
		private HttpRequestFixture httpRequestFixture;
		private static Random random = new Random();

		/// <summary>
		/// Initializes a new instance of the <see cref="RetrieveTests" /> class.
		/// </summary>
		/// <param name="requestFixture">The message store.</param>
		public RetrieveTests(HttpRequestFixture requestFixture)
		{
			this.httpRequestFixture = requestFixture;
		}

		/// <summary>
		/// TC-363092:Verify that a note can be retrieved with an attachment.
		/// </summary>
		/// <remarks>
		/// 1. Created an annotation with image file attachment.
		/// 2. Created an account and associated with the above created annotation.
		/// 3. Retrieved the above account and verified the attachment.
		/// </remarks>
		[Fact]
		public async void VerifyNoteCanBeRetrievedWithAttachment()
		{
			string currentTime = DateTime.UtcNow.ToString("yyMMddHHmmss");
			string accountId = string.Empty, annotationId = string.Empty;

			try
			{
				// To Create Annotation.
				string subjectName = "CRUDOnImageFileAttachment" + currentTime;

				// Binary value of the image is being added to documentBody.
				string filePath = @"Resources\TestImage.jpg";
				string documentBody = FileParserHelper.GetBinaryValueOfFileContent(filePath);
				var annotation = new
				{
					subject = subjectName,
					documentbody = documentBody,
					filename = "imageAttachment.jpg",
					mimetype = "image/jpeg"
				};

				annotationId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.annotations, annotation);
				Assert.True(annotationId != null);

				// To verify created annotation by retrieving.
				string annotationUri = "annotations(" + annotationId + ")?$select=subject,documentbody";
				JObject annotationRecord = await this.httpRequestFixture.GetEntityRecords(annotationUri);
				Assert.True(annotationRecord["documentbody"] != null && annotationRecord["documentbody"].ToString() == documentBody);

				// To Creae an Account.
				string accountName = "account" + currentTime;
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountName);

				// To associate annotation and account records.
				HttpResponseMessage associationResponse = await this.httpRequestFixture.AssociateEntityAsync(EntityLogicalName.accounts.ToString(), accountId, EntityLogicalName.annotations.ToString(), annotationId, "Account_Annotation");
				Assert.True(associationResponse.StatusCode == HttpStatusCode.NoContent);

				// To retrieve the annotation with attachment with account id.
				string accountAnnotationUri = "accounts(" + accountId + ")/Account_Annotation";
				JObject records = await this.httpRequestFixture.GetEntityRecords(accountAnnotationUri);
				Assert.True(records["value"][0]["documentbody"] != null && records["value"][0]["documentbody"].ToString() == documentBody);
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To clean up.
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.annotations, annotationId);
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// TC-363062:[Scorecard]Retrieve Complex Types
		/// </summary>
		/// <remarks>
		/// 1. Retrieved first saved query which is account
		/// 2. Verified the values of iscustomizable property value
		/// 3. Tried to retrieve the one of the properties of iscustomizable value.
		/// 4. Verified the expected 501 response
		/// </remarks>
		[Fact]
		public async void VerifyRetrieveComplexType()
		{
			// To retrieve a savedquery.
			string savedQueryUri = "savedqueries?$top=1";
			JObject savedQueryRecord = await this.httpRequestFixture.GetEntityRecords(savedQueryUri);

			// To verify the retrieved record
			string savedQueryId = savedQueryRecord["value"][0]["savedqueryid"].ToString();
			string isCustomizable = savedQueryRecord["value"][0]["iscustomizable"].ToString();
			Assert.True(isCustomizable.Contains("Value") && isCustomizable.Contains("CanBeChanged") && isCustomizable.Contains("ManagedPropertyLogicalName"));

			// To verify that the request gives 501 error, for get on properties of complex type.
			string complexTypePropertyUri = "savedqueries(" + savedQueryId + ")/iscustomizable/CanBeChanged";
			var response = await this.httpRequestFixture.ExecuteRequestForUrl(complexTypePropertyUri);
			Assert.True(response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
		}

		/// <summary>
		/// TC-480142:Retrieve all Primitive Types
		/// </summary>
		/// <remarks>
		/// 1. Retrieved existing business unit id of the org
		/// 2. Created a new child businessunit to the existing businessunit along with creditlimit value.
		/// 3. Retrieved the creditlimit value and verified for double type.
		/// 4. Retrieved the name value and verified for string type.
		/// 5. Created a contact with firstname and last name.
		/// 6. Created a account record along with primary contact id with the above contact along with number of employees value.
		/// 7. Retrieved the number of employees value and verified for integer.
		/// 8. Retrieved the followemail valueand verified for bool.
		/// 9. Created an annotation with image file as attachment.
		/// 10. Retrieved the attachment and verified for binary.
		/// </remarks>
		[Fact]
		public async void VerifyRetrieveAllPrimitiveTypes()
		{
			string businessUnitId = string.Empty, contactId = string.Empty, accountId = string.Empty, annotationId = string.Empty;
			string currentTime = DateTime.UtcNow.ToString("yyMMddHHmmss");

			try
			{
				// Fetching the existing businessunit id.
				JObject jsonResponse = await this.httpRequestFixture.GetEntityRecords("businessunits?$top=1");
				string baseBUId = jsonResponse["value"][0]["businessunitid"].ToString();

				// To create businessunit.
				string businessUnitName = "businessunit1" + currentTime;
				var businessunit = new JObject();
				businessunit.Add("name", businessUnitName);
				businessunit.Add("creditlimit", 1000);
				businessunit.Add("parentbusinessunitid@odata.bind", "businessunits(" + baseBUId + ")");
				businessUnitId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.businessunits, businessunit);

				// To retrieve creditlimit value of businessunit for double.
				string doubleTypeUri = "businessunits(" + businessUnitId + ")/creditlimit";
				JObject businessUnitRecord = await this.httpRequestFixture.GetEntityRecords(doubleTypeUri);
				JToken creditLimitRetrieved;
				businessUnitRecord.TryGetValue("value", out creditLimitRetrieved);
				Assert.True(Convert.ToDouble(creditLimitRetrieved.ToString()) == 1000);

				// To retrieve name of businessunit for string.
				string stringTypeUri = "businessunits(" + businessUnitId + ")/name";
				JObject businessUnitNameRecord = await this.httpRequestFixture.GetEntityRecords(stringTypeUri);
				JToken nameRetrieved;
				businessUnitNameRecord.TryGetValue("value", out nameRetrieved);
				Assert.True(businessUnitName == nameRetrieved.ToString());

				// To create contact record.
				string lastName = "primitivetest" + currentTime, firstName = "contact1" + currentTime;
				contactId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.contacts, firstName, lastName);

				// To create account record.
				string accountName = "account1" + currentTime;
				JObject account = new JObject();
				account.Add("name", accountName);
				account.Add("primarycontactid@odata.bind", "/contacts(" + contactId + ")");
				account.Add("numberofemployees", 150);
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.accounts, account);

				// To retrieve primary contact id of account for GUID.
				string guidTypeUri = "accounts(" + accountId + ")/_primarycontactid_value";
				JObject accountPrimaryContactRecord = await this.httpRequestFixture.GetEntityRecords(guidTypeUri);
				JToken primaryContactRetrieved;
				accountPrimaryContactRecord.TryGetValue("value", out primaryContactRetrieved);
				Guid accountPrimaryContactIdRetrieved = new Guid(primaryContactRetrieved.ToString());
				Guid createdContactId = new Guid(contactId);
				Assert.True(createdContactId == accountPrimaryContactIdRetrieved);

				// To retrieve number of employees of account for int.
				string intTypeUri = "accounts(" + accountId + ")/numberofemployees";
				JObject numberOfEmployeesRecord = await this.httpRequestFixture.GetEntityRecords(intTypeUri);
				JToken numberOfEmployeesRetrieved;
				numberOfEmployeesRecord.TryGetValue("value", out numberOfEmployeesRetrieved);
				Assert.True(Convert.ToInt16(numberOfEmployeesRetrieved.ToString()) == 150);

				// To retrieve followemail of account for bool.
				string boolTypeUri = "accounts(" + accountId + ")/followemail";
				JObject followemailRecord = await this.httpRequestFixture.GetEntityRecords(boolTypeUri);
				JToken followemailRetrieved;
				followemailRecord.TryGetValue("value", out followemailRetrieved);
				Assert.True(Convert.ToBoolean(followemailRetrieved.ToString()));

				// To Create Annotation.
				string subjectName = "CRUDOnImageFileAttachment" + currentTime;

				// Binary value of the image is being added to documentBody.
				string filePath = @"Resources\TestImage.jpg";
				string documentBody = FileParserHelper.GetBinaryValueOfFileContent(filePath);
				var annotation = new
				{
					subject = subjectName,
					documentbody = documentBody,
					filename = "imageAttachment.jpg",
					mimetype = "image/jpeg"
				};

				annotationId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.annotations, annotation);
				Assert.True(annotationId != null);

				// To verify created annotation by retrieving for binary.
				string annotationUri = "annotations(" + annotationId + ")/documentbody";
				JObject annotationRecord = await this.httpRequestFixture.GetEntityRecords(annotationUri);
				Assert.True(annotationRecord["value"] != null && annotationRecord["value"].ToString() == documentBody);
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To clean up.
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.annotations, annotationId);
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.contacts, contactId);
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);

				// To disable business unit and delete.
				if(!string.IsNullOrEmpty(businessUnitId))
				{
					await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(new HttpMethod("PATCH"), "businessunits(" + businessUnitId + ")", JObject.Parse("{isdisabled:'true'}"), HttpStatusCode.NoContent);
					await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.businessunits, businessUnitId);
				}
			}
		}

		/// <summary>
		/// TC-363059:Retrieve the top 3 quotes related to an account and expand the quotes ordered by Name
		/// </summary>
		/// <remarks>
		/// 1. created an account record.
		/// 2. created three contacts and associated them with above contact record.
		/// 3. Retreived the contacts with $expand and $order by lastname of the contact.
		/// 4. Verified the order of the results with order of the creation.
		/// </remarks>
		[Fact]
		public async void VerifyExpandOrderByWithAccountAndQuotes()
		{
			string currentTime = DateTime.UtcNow.ToString("yyMMddHHmmss");
			List<string> contactIds = new List<string>();
			string accountId = string.Empty;

			try
			{
				// To create account.
				string accountName = "account1" + currentTime;
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountName);

				// To create 3 contacts and associate with above created account.
				for (int i = 1; i <= 3; i++)
				{
					string contactName = "contact_" + i.ToString() + "_" + currentTime;
					string firstName = "firstname_" + i.ToString() + "_" + currentTime;
					string lastName = "lastname_" + i.ToString() + "_" + currentTime;
					string contactId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.contacts, firstName, lastName);
					await this.httpRequestFixture.AssociateEntityAsync("accounts", accountId, "contacts", contactId, "contact_customer_accounts");
					contactIds.Add(contactId);
				}

				// Retrieve Account name, number of employees , contact first name, last name and full name.
				string retrievePath = "accounts(" + accountId + ")?$expand=contact_customer_accounts($select=lastname,contactid;$orderby=lastname asc;$top=3)&$select=name,accountid";
				JObject accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);

				// To verify the order of the results.
				var associatedContacts = JArray.FromObject(accounts["contact_customer_accounts"]);
				List<string> contactsRetrieved = (from contact in associatedContacts where associatedContacts.HasValues select contact["contactid"].ToString()).ToList();
				Assert.True(contactsRetrieved.SequenceEqual<string>(contactIds));
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To clean up.
				foreach(string contactId in contactIds)
				{
					await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.contacts, contactId);
					await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
				}
			}
		}

		/// <summary>
		/// Automated TC363106 - ABNF:Preferences - maxpagesize - ok
		/// </summary>
		/// <remarks>
		/// 1 - Create 7 accounts
		/// 2 - Fire a GET request header 'Prefer' : 'odata.maxpagesize=5'.
		/// 3 - Verify that only 5 accounts have been returned in the response
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyMaxPageSize()
		{
			List<string> accountIds = new List<string>();

			try
			{
				// Create 7 accounts
				for (int i = 0; i < 7; i++)
				{
					string accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "TC363106_Account" + i);
					accountIds.Add(accountId);
				}

				// Fire a GET request to retrieve accounts and in header 'Prefer' : 'odata.maxpagesize=5'.
				Dictionary<string, string> header = new Dictionary<string, string>();
				header.Add("Prefer", "odata.maxpagesize=5");
				var accountsData = await this.httpRequestFixture.GetEntityRecords(EntityLogicalName.accounts.ToString(), header);

				// Verify that only 5 records are returned in the response
				Assert.True(accountsData["value"].Count() == 5, "Only 5 records should be returned");
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				foreach (string accountId in accountIds)
				{
					await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
				}
			}
		}

		/// <summary>
		/// Automated TC480192 - ABNF: 4.7 Addressing a Property Value - with $format
		/// </summary>
		/// <remarks>
		/// 1 - Create contact
		/// 2 - Add header ("odata-maxversion", "4.0")
		/// 2 - Fire a GET request to retrieve contact id in json format.
		/// 3 - Verify contact id retrieved is equal to the id of contact created
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyAddressingPropertyValueWithFormat()
		{
			string contactId = string.Empty;

			try
			{
				// Create Contact
				contactId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.contacts, "TC480192", "contact");

				// Add header
				Dictionary<string, string> header = new Dictionary<string, string>();
				header.Add("odata-maxversion", "4.0");

				// Verify addressing property value with $format
				string retrievePath = "contacts(" + contactId + ")/contactid/$value?$format=json";
				var response = await this.httpRequestFixture.ExecuteRequestForUrl(retrievePath, header);
				var responseContactId = response.Content.ReadAsStringAsync().Result;
				Assert.True(responseContactId == contactId);
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.contacts, contactId);
			}
		}

		/// <summary>
		/// Automated TC480191 - ABNF: 4.7 Addressing a Property Value
		/// </summary>
		/// <remarks>
		/// 1 - Create contact
		/// 2 - Add header ("odata-maxversion", "4.0")
		/// 2 - Fire a GET request to retrieve contact id.
		/// 3 - Verify contact id retrieved is equal to the id of contact created
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyAddressingPropertyValue()
		{
			string contactId = string.Empty;

			try
			{
				// Create Contact
				contactId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.contacts, "TC480191", "contact");

				// Add header
				Dictionary<string, string> header = new Dictionary<string, string>();
				header.Add("odata-maxversion", "4.0");

				// Verify addressing property value
				string retrievePath = "contacts(" + contactId + ")/contactid/$value";
				var response = await this.httpRequestFixture.ExecuteRequestForUrl(retrievePath, header);
				var responseContactId = response.Content.ReadAsStringAsync().Result;
				Assert.True(responseContactId == contactId);
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.contacts, contactId);
			}
		}

		/// <summary>
		/// Automated TC480136 - Get all accounts that have Credit Limit ge 1000 and are from Texas
		/// </summary>
		/// <remarks>
		/// 1 - Create account with credit limit greater than 1000 and state with "TX".
		/// 2 - Fire a GET request to retrieve accounts with credit limit greater than 1000 and state with "TX".
		/// 3 - Verify accounts with credit limit greater than 1000 and state with "TX" are retrieved successfully.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyCreditLimitAndState()
		{
			string accountId = string.Empty;

			try
			{
				// Create an account with credit limit and stateorprovince
				var account = new JObject();
				account["creditlimit"] = 1200;
				account["address1_stateorprovince"] = "TX";
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.accounts, account);

				// Retrieve accounts with credit limit greater than 1000 and state with "TX"
				string retrievePath = "accounts?$filter=creditlimit ge 1000 and address1_stateorprovince eq 'TX'";
				JObject accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				JArray accountsArray = JArray.FromObject(accounts["value"]);
				Assert.True(accountsArray.Where(item => Convert.ToInt16(item["creditlimit"].ToString()) < 1000 && (item["address1_stateorprovince"].ToString()) != "TX").Count() == 0, "Expected 0 items but returned more");
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// Automated TC480175 - $count - With $filter
		/// </summary>
		/// <remarks>
		/// 1 - Create 3 accounts which starts with letter 'vTC_480175'.
		/// 2 - Fire a GET request to retrieve accounts which starts with letter 'vTC_480175' and its count.
		/// 3 - Verify accounts retrieved count is greater than or equal to accounts created in this method.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyCountWithFilter()
		{
			List<string> accountIds = new List<string>();
			string accountName = "vTC_480175";

			try
			{
				// Create 3 accounts which starts with letter 'vTC_480175'
				for (int i = 0; i < 3; i++)
				{
					string accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountName + i);
					accountIds.Add(accountId);
				}

				// Retrieve accounts which starts with letter 'vTC_480175'
				string retrievePath = "accounts?$count=true&$filter=startswith(name,'" + accountName + "')";
				JObject accountRecords = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				int count = int.Parse(accountRecords["@odata.count"].ToString());
				Assert.True(count >= 3);
			}
			catch (Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				foreach (string accountId in accountIds)
				{
					await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
				}
			}
		}

		/// <summary>
		/// TC480169:-ABNF:Context URL - Entity set with type cast
		/// </summary>
		/// <remarks>
		/// 1.Create a query for entity set with type cast
		/// 2.Get the record using the query
		/// 3.Verify if the coontext URL contains typecast
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async Task VerifyEntitySetWithTypeCast()
		{
			string relativepath = "activitypointers/Microsoft.Dynamics.CRM.email";
			var response = await this.httpRequestFixture.GetEntityRecords(relativepath);
			Assert.True(CompareContextUrl(response, "/$metadata#activitypointers"), "Context URL for Entity set with type cast is wrong");
		}

		/// <summary>
		/// TC480168:-ABNF:Context URL - Entity with $select and $expand with nested select
		/// </summary>
		/// <remarks>
		/// 1.Create an account
		/// 2.Create a contact
		/// 3.Create an association contact_customer_accounts between them
		/// 4.Create a query for entity with $select and $expand with nested select
		/// 5.Verify the context URL conatins nested metadata collection for the entity
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async Task VerifyEntityWithSelectAndExpand()
		{
			string accountId = string.Empty;

			try
			{
				string contactId = string.Empty;
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "acc" + RandomString(5));
				contactId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.contacts, "con", RandomString(4));
				var association = await this.httpRequestFixture.AssociateEntityAsync(EntityLogicalName.accounts.ToString(), accountId, EntityLogicalName.contacts.ToString(), contactId, "contact_customer_accounts");
				string relativepath = string.Format("accounts({0})?$select=name,accountid,contact_customer_accounts&$expand=contact_customer_accounts($select=fullname,contactid)", accountId);
				var response = await this.httpRequestFixture.GetEntityRecords(relativepath);
				Assert.True(CompareContextUrl(response, "/$metadata#accounts(name,accountid,contact_customer_accounts(fullname,contactid))/$entity"), "Context URL for EntitySet with $select and $expand with nested select is wrong");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// TC480167:-ABNF:Context URL - Entity set with $select
		/// </summary>
		/// <remarks>
		/// 1.Create an query for entity set with select
		/// 2.Get the data using the query
		/// 3.Check if Context URL contains entity set with fields
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async Task VerifyEntitySetWithSelect()
		{
			string relativepath = "accounts?$select=name,numberofemployees";
			var response = await this.httpRequestFixture.GetEntityRecords(relativepath);
			Assert.True(CompareContextUrl(response, "/$metadata#accounts(name,numberofemployees)"), "Context URL for EntitySet with $select is wrong");
		}

		/// <summary>
		/// TC480166:-ABNF:Context URL - Entity set
		/// </summary>
		/// <remarks>
		/// 1.Get the entity set using the query for entities
		/// 2.Verify if the context URL has value $metadata#entity
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async Task VerifyEntitySet()
		{
			var response = await this.httpRequestFixture.GetEntityRecords("accounts");
			Assert.True(CompareContextUrl(response, "/$metadata#accounts"), "Context URL for EntitySet is wrong");
		}

		/// <summary>
		/// TC363076:-ABNF:5.1.2 Expand - no $select after $ref
		/// </summary>
		/// <remarks>
		/// 1.Create an account
		/// 2.Create a contact
		/// 3.Create an association account_primary_contact between both
		/// 4.Create a query with select after ref
		/// 5.Get the response using the query
		/// 6.Verify bad request response is retrieved
		/// </remarks>
		/// <returns>Task Object</returns>
		[Fact]
		public async Task VerifyExpandNoSelectAfterRef()
		{
			string accountId = string.Empty;
			string contactId = string.Empty;

			try
			{
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "acc" + RandomString(5));
				contactId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.contacts, "con", RandomString(4));
				var association = await this.httpRequestFixture.AssociateEntityAsync(EntityLogicalName.contacts.ToString(), contactId, EntityLogicalName.accounts.ToString(), accountId, "account_primary_contact");
				string relativepath = string.Format("accounts({0})/account_primary_contact/$ref ($select = fullname)", accountId);
				var response = await this.httpRequestFixture.ExecuteRequestForUrl(relativepath);
				Assert.True(response.StatusCode == HttpStatusCode.BadRequest, "The relativepath is correct");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.contacts, contactId);
			}
		}

		/// <summary>
		/// Automated TC362974 - [RM:Expand]Verify expand on multilookup N:1 navigation property
		/// </summary>
		/// <remarks>
		/// 1.Create 3 emails with account
		/// 2.Create a query with expand regardingobjectid_account_email
		/// 3.Get Subject,accountid associated with email
		/// 4.Check if it is equal to the 3 record we have created.
		/// </remarks>
		/// <returns>Task Object</returns>
		[Fact]
		public async Task VerifyExpandAccountEmail()
		{
			List<string> accountIds = null;

			try
			{
				int emailAct = 3;
				string key = DateTime.UtcNow.Ticks.ToString();
				await CreateEmailWithAccount(emailAct, key);
				string requesturl = String.Format("emails?$filter=endswith(subject,'{0}')&$select=subject,_regardingobjectid_value&$expand=regardingobjectid_account_email($select=name,accountid)", key);
				var response = await this.httpRequestFixture.GetEntityRecords(requesturl);
				int emailSubjectVerificationCount = response["value"].Select(x => x.SelectTokens("subject")).Count();
				var emailAccountIds = response["value"].Select(x => x.SelectTokens("_regardingobjectid_value").FirstOrDefault().ToString()).ToList();
				accountIds = response["value"].Select(x => x.SelectTokens("regardingobjectid_account_email")).Select(y => y.Values("accountid").FirstOrDefault().ToString()).ToList();
				int navPropertyVerificationCount = accountIds.Intersect(emailAccountIds).Count();
				Assert.True(emailSubjectVerificationCount == emailAct && navPropertyVerificationCount == emailAct, "Failure Reason:no of Navigationproperty or no of emails are different");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordByIds(EntityLogicalName.accounts, accountIds);
			}
		}

		/// <summary>
		/// Automated TC362972 - [RM:Expand]Verify that N:1 navigation properties can be expanded on entity set (OOB entity : OOB entity)
		/// </summary>
		/// <remarks>
		/// 1.Create 3 accounts with primary contact
		/// 2.Create a query with expand primarycontactid
		/// 3.Get name,and primarycontactid properties
		/// 4.Check if the values are equal to the 3 record we have created.
		/// </remarks>
		/// <returns>Task Object</returns>
		[Fact]
		public async Task VerifyExpandAccountContact()
		{
			List<string> accountIds = null;
			List<string> contactIds = null;

			try
			{
				string key = DateTime.UtcNow.Ticks.ToString();
				int countAct = 3;
				accountIds = await CreateAccountsWithPrimaryContact(countAct, key);
				string requesturl = String.Format("accounts?$filter=endswith(name,'{0}')&$select=name&$expand=primarycontactid($select=lastname,emailaddress1,telephone1,contactid)", key);
				var response = await this.httpRequestFixture.GetEntityRecords(requesturl);
				int accountNameVerificationcount = response["value"].Where(x => x.SelectTokens("name").FirstOrDefault().ToString().Contains(key)).Count();
				var contactRecords = response["value"].Select(x => x.SelectTokens("primarycontactid")).Where(y => y.Values("lastname").FirstOrDefault().ToString().Contains(key) && y.Values("emailaddress1").FirstOrDefault().ToString().Contains(key) && y.Values("telephone1").FirstOrDefault().ToString().Contains(key));
				contactIds = contactRecords.Select(x => x.Values("contactid").FirstOrDefault().ToString()).ToList();
				int navPropertyVerificationCount = contactIds.Count();
				Assert.True(accountNameVerificationcount == countAct && navPropertyVerificationCount == countAct, "Failure Reason:no of accounts with key name or no of navigation property are different");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordByIds(EntityLogicalName.accounts, accountIds);
				await this.httpRequestFixture.DeleteEntityRecordByIds(EntityLogicalName.contacts, contactIds);
			}
		}

		/// <summary>
		/// Automated TC362970 - [RM:Expand]Verify that 1:N navigation properties can be expanded on entity set (OOB entity : OOB entity)
		/// </summary>
		/// <remarks>
		/// 1.Create 3 accounts with primary contact
		/// 2.Create a query with expand primarycontactid
		/// 3.Get name,and primarycontactid properties
		/// 4.Check if the values are equal to the 3 record we have created.
		/// </remarks>
		/// <returns>Task Object</returns>
		[Fact]
		public async Task VerifyExpandContactAccounts()
		{
			string key = DateTime.UtcNow.Ticks.ToString();
			int countAct = 3;
			int countCont = 3;
			List<string> accountIds = null;

			try
			{
				accountIds = await CreateAccountsWithContacts(countAct, countCont, key);

				// Scenario 1
				{
					string requesturl = String.Format("accounts?$filter=endswith(name,'{0}')&$select=name&$expand=contact_customer_accounts", key);
					var response = await this.httpRequestFixture.GetEntityRecords(requesturl);
					int navpropverificationcount1 = response["value"].Select(x => x.SelectToken("contact_customer_accounts")).Where(y => y.Count() == 0).Count();
					int navpropverificationcount2 = response["value"].Where(x => x.SelectToken("['contact_customer_accounts@odata.nextLink']").ToString().EndsWith(String.Format("accounts({0})/contact_customer_accounts", x.SelectToken("accountid").ToString()))).Count();
					Assert.True(navpropverificationcount1 == countAct & navpropverificationcount2 == countAct, "Failure Reason:no of nav property is different");
				}

				// Scenario 2
				{
					string requesturl = String.Format("accounts?$filter=endswith(name,'{0}')&$expand=contact_customer_accounts($select=lastname,emailaddress1,telephone1)", key);
					var response = await this.httpRequestFixture.GetEntityRecords(requesturl);
					int navpropverificationcount1 = response["value"].Select(x => x.SelectToken("contact_customer_accounts")).Where(y => y.Count() == 0).Count();
					int navpropverificationcount2 = response["value"].Where(x => x.SelectToken("['contact_customer_accounts@odata.nextLink']").ToString().EndsWith(String.Format("accounts({0})/contact_customer_accounts?$select=lastname,emailaddress1,telephone1", x.SelectToken("accountid").ToString()))).Count();
					Assert.True(navpropverificationcount1 == countAct & navpropverificationcount2 == countAct, "Failure Reason:no of nav property is different");
				}
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordByIds(EntityLogicalName.accounts, accountIds);
			}
		}

		/// <summary>
		/// TC480176:-ABNF:Context URL - Reference
		/// </summary>
		/// <remarks>
		/// 1.Create an account associate with a contact
		/// 2.Create a query having contact_customer_accounts/$ref
		/// 3.Verify the response context have api/data/v9.0/$metadata#Collection($ref)
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async Task VerifyContextUrl()
		{
			string accountId = string.Empty;

			try
			{
				JObject contact = new JObject();
				contact["firstname"] = "con";
				contact["lastname"] = RandomString(5);
				JArray contacts = new JArray();
				contacts.Add(contact);
				JObject account = new JObject();
				account["name"] = "acc" + RandomString(4);
				account["contact_customer_accounts"] = contacts;
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync<JObject>(HttpMethod.Post, EntityLogicalName.accounts, account);
				string path = string.Format("accounts({0})/contact_customer_accounts/$ref", accountId);
				var response = await this.httpRequestFixture.GetEntityRecords(path);
				Assert.True(CompareContextUrl(response, "/$metadata#Collection($ref)"), "Failure Reason:-Context URL for reference is not correct");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// TC480174:-ABNF:Context URL - Individual collection value
		/// </summary>
		/// <remarks>
		/// 1.Create an account
		/// 2.Get the individual collection value name using the select query
		/// 3.Verify URL context has $metadata#accounts(name)
		/// </remarks>
		/// <returns>Task object</returns>
		[Fact]
		public async Task VerifyIndividualCollectionValue()
		{
			string accountId = string.Empty;

			try
			{
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "acc" + RandomString(5));
				string relativepath = string.Format("accounts({0})?$select=name", accountId);
				var response = await this.httpRequestFixture.GetEntityRecords(relativepath);
				Assert.True(CompareContextUrl(response, "/$metadata#accounts(name)"), "Failure Reason:-Context URL for Individual collection value is not correct");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// TC480173:-ABNF:Context URL - Entity property value with $select
		/// </summary>
		/// <remarks>
		/// 1.Create an account
		/// 2.Get the account name using select
		/// 3.Verify if the context URL contains entity property value
		/// </remarks>
		/// <returns>Task object</returns>
		[Fact]
		public async Task VerifyEntityPropvalueWithSelect()
		{
			string accountId = string.Empty;

			try
			{
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "acc" + RandomString(5));
				string relativepath = string.Format("accounts({0})?$select=name", accountId);
				var response = await this.httpRequestFixture.GetEntityRecords(relativepath);
				Assert.True(CompareContextUrl(response, "/$metadata#accounts(name)/$entity"), "Failure Reason:-Context URL for Individual collection value is not correct");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// TC480172:-ABNF:Context URL - Entity property value
		/// </summary>
		/// <remarks>
		/// 1.Create an account
		/// 2.Get the account name using query
		/// 3.Verify if the URL context has entity property value
		/// </remarks>
		/// <returns>Task Object</returns>
		[Fact]
		public async Task VerifyEntityPropValue()
		{
			string accountId = string.Empty;

			try
			{
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "acc" + RandomString(5));
				string relativepath = string.Format("accounts({0})/name", accountId);
				var response = await this.httpRequestFixture.GetEntityRecords(relativepath);
				Assert.True(CompareContextUrl(response, string.Format("/$metadata#accounts({0})/name", accountId)), "Failure Reason:-Context URL for Entity property value is not correct");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// TC480171:-ABNF:Context URL - Entity with $select
		/// </summary>
		/// <remarks>
		/// 1.Create an account
		/// 2.Get the records with multiple collection using select query
		/// 3.Verify the UrlContext has multiple entity property value
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async Task VerifyContextUrlEntityWithSelect()
		{
			string accountId = string.Empty;

			try
			{
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "acc" + RandomString(5));
				string relativepath = string.Format("accounts({0})?$select=name,accountid", accountId);
				var response = await this.httpRequestFixture.GetEntityRecords(relativepath);
				Assert.True(CompareContextUrl(response, string.Format("/$metadata#accounts(name,accountid)/$entity", accountId)), "Failure Reason:-Context URL for Entity property value is not correct");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// TC363082:-ABNF:Context URL - collection with containment
		/// </summary>
		/// <remarks>
		/// 1.Create an account having contact 
		/// 2.Get the associated contact using contact_customer_accounts property
		/// 3.Verify the Context URL has $metadata#contacts
		/// 4.Associate the same record as primarycontactid of account
		/// 5.Verify the context URL has $metadata#contacts/$entity
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async Task VerifyContextUrlForCollectionWithContainment()
		{
			string accountId = string.Empty;

			try
			{
				JObject contact = new JObject();
				contact["firstname"] = "con";
				contact["lastname"] = RandomString(5);
				JArray contacts = new JArray();
				contacts.Add(contact);
				JObject account = new JObject();
				account["name"] = "acc" + RandomString(4);
				account["contact_customer_accounts"] = contacts;
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync<JObject>(HttpMethod.Post, EntityLogicalName.accounts, account);
				string path = string.Format("accounts({0})/contact_customer_accounts", accountId);
				var response = await this.httpRequestFixture.GetEntityRecords(path);
				string contactId = response["value"].Select(x => x.SelectToken("contactid")).FirstOrDefault().ToString();
				Assert.True(CompareContextUrl(response, "/$metadata#contacts"), "Failure Reason:-Context URL for reference contact is not correct");
				await this.httpRequestFixture.AssociateEntityAsync(EntityLogicalName.contacts.ToString(), contactId, EntityLogicalName.accounts.ToString(), accountId, "account_primary_contact");
				string relativepath = string.Format("accounts({0})/primarycontactid", accountId);
				var response1 = await this.httpRequestFixture.GetEntityRecords(relativepath);
				Assert.True(CompareContextUrl(response1, "/$metadata#contacts/$entity"), "Failure Reason:-Context URL for reference primary contact is not correct");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// TC480170:-ABNF:Context URL - Entity
		/// </summary>
		/// <remarks>
		/// 1.Create an account
		/// 2.Get the record with the query.
		/// 3.Check the context url has entity.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async Task VerifyUrlContextEntity()
		{
			string accountId = string.Empty;

			try
			{
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "acc" + RandomString(5));
				string relativepath = string.Format("accounts({0})", accountId);
				var response = await this.httpRequestFixture.GetEntityRecords(relativepath);
				Assert.True(CompareContextUrl(response, "/$metadata#accounts/$entity"), "Failure Reason:-Context URL for entity is not correct");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// Automated TC363139 - ABNF: 4.4 Addressing Links between Entities
		/// </summary>
		/// <remarks>
		/// 1.create a contact and associated account.
		/// 2.Get the contactid created
		/// 3.Raise a get request having invalid URL on navigation property contact_customer_accounts
		/// 4.Raise a get request with valid URL on nav property
		/// 5.Raise a get request having invalid URL on nav property but different type
		/// 6.Verify the status code of first and last as BadRequest and NotFound while middle one returning exact contact value
		/// </remarks>
		/// <returns>Task object</returns>
		[Fact]
		public async Task VerifyLinksBetEntities()
		{
			string accountId = string.Empty;
			string contactId = string.Empty;

			try
			{
				string retrievedContactId = string.Empty;
				JObject contact = new JObject();
				contact["firstname"] = "con";
				contact["lastname"] = RandomString(4);
				JArray contacts = new JArray();
				contacts.Add(contact);
				JObject account = new JObject();
				account["name"] = "acc" + RandomString(4);
				account["contact_customer_accounts"] = contacts;
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync<JObject>(HttpMethod.Post, EntityLogicalName.accounts, account);
				var contactresponse = await this.httpRequestFixture.GetEntityRecords("contacts?$select=contactid");
				contactId = contactresponse["value"].LastOrDefault().LastOrDefault().FirstOrDefault().ToString();

				// Scenario1 : Verify exception when passing invalid URL on navigation properties
				string relativePath1 = string.Format("accounts({0})/contact_customer_accounts/$ref?$id=contacts({1})", accountId, contactId);
				var response1 = await this.httpRequestFixture.ExecuteRequestForUrl(relativePath1);
				Assert.True(response1.StatusCode == System.Net.HttpStatusCode.BadRequest, "Failure Reason:must be invalid URL on navigation properties");

				// Scenario2 : Verify retrieve on navigation property using $ref returned as expected
				string relativePath2 = string.Format("accounts({0})/contact_customer_accounts/$ref", accountId);
				var response2 = await this.httpRequestFixture.GetEntityRecords(relativePath2);
				string record = response2["value"][0]["@odata.id"].ToString();
				Match match = Regex.Match(record, @"([a-z0-9]{8}[-][a-z0-9]{4}[-][a-z0-9]{4}[-][a-z0-9]{4}[-][a-z0-9]{12})");
				if (match.Success)
				{
					retrievedContactId = match.Groups[1].Value;
				}

				Assert.True(retrievedContactId == contactId, "Failure Reason:retrieve on navigation property using $ref should be valid URL");

				// Scenario3 : Verify exception when passing invalid URL on navigation properties
				string relativePath3 = string.Format("accounts({0})/contact_customer_accounts/$ref/$count", accountId);
				var response3 = await this.httpRequestFixture.ExecuteRequestForUrl(relativePath3);
				Assert.True(response3.StatusCode == System.Net.HttpStatusCode.NotFound, "Failure Reason: must be valid URL on navigation properties");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// Automated TC363136 - ABNF: 2 URL Components - resource path and query options
		/// </summary>
		/// <remarks>
		/// 1.Create an account having two associated contacts
		/// 2.sort the contacts array using fullname
		/// 3.Raise a get requset to get contact_customer_accounts filter as top 2 records and orderby=fullname
		/// 4.check the sorted response and the sorted array have full name matching.
		/// </remarks>
		/// <returns>Task object</returns>
		[Fact]
		public async Task VerifyOrderOfRelatedEntities()
		{
			string accountId = string.Empty;

			try
			{
				JArray contacts = new JArray();
				for (int i = 0; i < 2; i++)
				{
					JObject contact = new JObject();
					contact["firstname"] = "con";
					contact["lastname"] = RandomString(4);
					contacts.Add(contact);
				}

				var sortedContacts = contacts.OrderBy(x => x["firstname"].ToString() + x["lastname"]).Select(x => x["firstname"].ToString() + " " + x["lastname"]).ToArray();
				JObject account = new JObject();
				account["name"] = "acc" + RandomString(4);
				account["contact_customer_accounts"] = contacts;
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync<JObject>(HttpMethod.Post, EntityLogicalName.accounts, account);
				string query = string.Format("accounts({0})/contact_customer_accounts?$top=2&$orderby=fullname", accountId);
				var response = await this.httpRequestFixture.GetEntityRecords(query);
				var retrievedSortedContacts = response["value"].Select(x => x.SelectToken("fullname").ToString()).ToArray();
				var diff = retrievedSortedContacts.Except(sortedContacts);
				Assert.True(retrievedSortedContacts.Count() == 2, "Failure Reason:-top 2 records didnt retrieved");
				Assert.True(diff.Count() == 0, "Failure Reason:-top 2 records are not sorted as per fullname");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// TC480154: Derived Entity Collection - Retrieve an Activity Collection - Entity Agnostic
		/// </summary>
		/// <remarks>
		/// 1.Create 3 task activities
		/// 2.create an account
		/// 3.associate 3 tasks with created account
		/// 4.Raise a get call to return Account_ActivityPointers count.
		/// 5.check if it matches with no of created tasks
		/// </remarks>
		/// <returns>Task Object</returns>
		[Fact]
		public async Task VerifyAssociatedRecords()
		{
			string accountId = string.Empty;

			try
			{
				JObject task = JObject.Parse("{subject:'" + "mysub" + "'}");
				var taskActivityIdOne = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.tasks, task);
				var taskActivityIdTwo = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.tasks, task);
				var taskActivityIdThree = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.tasks, task);

				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "testAccount");

				await this.httpRequestFixture.AssociateEntityAsync(EntityLogicalName.accounts.ToString(), accountId, EntityLogicalName.tasks.ToString(), taskActivityIdOne, "Account_ActivityPointers");
				await this.httpRequestFixture.AssociateEntityAsync(EntityLogicalName.accounts.ToString(), accountId, EntityLogicalName.tasks.ToString(), taskActivityIdTwo, "Account_ActivityPointers");
				await this.httpRequestFixture.AssociateEntityAsync(EntityLogicalName.accounts.ToString(), accountId, EntityLogicalName.tasks.ToString(), taskActivityIdThree, "Account_ActivityPointers");

				var res = await this.httpRequestFixture.GetEntityRecordCount(string.Format("accounts({0})/Account_ActivityPointers", accountId));

				Assert.True(res == 3, "Couldn't retireve all associates tasks");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// Automated TC480188 - ABNF: 2 URL Components - query options
		/// </summary>
		/// <remarks>
		/// 1.Create 3 accounts
		/// 2.Create a sorted dictionary ordered by name.
		/// 3.Create a query to get the top 2 records ordered by name ascending
		/// 4.Get the accounts using the query
		/// 5.Top 2 accounts which are ordered by name should be returned and matched with sorted dictionary
		/// </remarks>
		/// <returns>Task Object</returns>
		[Fact]
		public async Task VerifyURLComponentsQueryOptions()
		{
			Dictionary<string, string> trackingRecordList = new Dictionary<string, string>();

			try
			{
				string key = DateTime.UtcNow.Ticks.ToString();

				// Create 3 records of account
				for (int i = 0; i < 3; i++)
				{
					string name = "ac" + key + RandomString(2);
					string accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, name);
					trackingRecordList.Add(accountId, name);
				}

				// Sort the stored records
				var sortedAccountIds = trackingRecordList.OrderBy(x => x.Value).Select(y => y.Key.ToString()).ToList().Take(2);
				string query = string.Format("{0}?$select=name,accountid&$filter=contains(name,{1})&$orderby=name asc&$top=2", EntityLogicalName.accounts, "'ac" + key + "'");
				var response = await this.httpRequestFixture.GetEntityRecords(query);
				var retrieveAccounts = response["value"].Select(x => x.SelectToken("accountid").ToString()).ToList();
				Assert.True(retrieveAccounts.Intersect(sortedAccountIds).Count() == 2, "Failure Reason:-top 2 accounts which are ordered by name are not retrieved");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordByIds(EntityLogicalName.accounts, trackingRecordList.Keys.ToList());
			}
		}

		/// <summary>
		/// Automated TC480124 - OData: Retrieve all Accounts
		/// </summary>
		/// <remarks>
		/// 1.Create a account
		/// 2.Get the count of the created records
		/// 3.Verify the count should be one
		/// </remarks>
		/// <returns>Task Object</returns>
		[Fact]
		public async Task VerifyAccountsGet()
		{
			string accountId = string.Empty;
			try
			{
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "TAC" + RandomString(4));
				int accountsCount = await this.httpRequestFixture.GetEntityRecordCount(EntityLogicalName.accounts.ToString());
				Assert.True(accountsCount >= 1, "Failure Reason:-Getting Accounts request failed ");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// Automated TC480149 - OData: Verify that "eq" can be used in a $filter operator for primitive types along with $top and $orderby
		/// </summary>
		/// <remarks>
		/// 1.Create 4 accounts with different properties. 
		/// 2.Create two queries using order by,top and eq
		/// 3.fire the first query which checks eq on int type 
		/// 4.fire the second query which checks eq on string type 
		/// 5.Count should be same as the properties specified
		/// </remarks>
		/// <returns>Task Object</returns>
		[Fact]
		public async Task VerifyEqualForPrimitiveTypes()
		{
			Dictionary<string, JObject> trackingRecordList = new Dictionary<string, JObject>();
			try
			{
				string key = DateTime.UtcNow.Ticks.ToString();

				// Create 4 accounts 2 having fax same but numberofemployees not same,other two completely different
				for (int i = 0; i < 4; i++)
				{
					JObject account = new JObject();
					account["name"] = "ac" + key + RandomString(2);
					if (i % 2 != 0)
					{
						account["numberofemployees"] = 252 + random.Next(100, 1000);
						account["fax"] = "fax" + RandomString(3);
					}
					else
					{
						account["numberofemployees"] = 250 + i;
						account["fax"] = "faxsame";
					}

					string accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync<JObject>(HttpMethod.Post, EntityLogicalName.accounts, account);
					trackingRecordList.Add(accountId, account);
				}

				// Int32
				string query = string.Format("{0}?$select=name,accountid,numberofemployees&$orderby=numberofemployees asc&$top=2&$filter=numberofemployees eq {1} and contains(name,{2})", EntityLogicalName.accounts, 250, "'ac" + key + "'");
				var response = await this.httpRequestFixture.GetEntityRecords(query);
				Assert.True(response["value"].Count() == 1, "Failure Reason:-equal verification along with orderby and top returns wrong for int type");

				// String
				string query1 = string.Format("{0}?$select=name,accountid,numberofemployees&$orderby=numberofemployees asc&$top=2&$filter=fax eq 'faxsame' and contains(name,{1})", EntityLogicalName.accounts, "'ac" + key + "'");
				var response2 = await this.httpRequestFixture.GetEntityRecords(query1);
				Assert.True(response2["value"].Count() == 2, "Failure Reason:-equal verification along with orderby and top returns wrong for string type");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordByIds(EntityLogicalName.accounts, trackingRecordList.Keys.ToList());
			}
		}

		/// <summary>
		/// Create multiple email with account
		/// </summary>
		/// <param name="emailAct">no of emails with account to create</param>
		/// <param name="key">identifier</param>
		/// <returns>List of emailIds</returns>
		public async Task<List<string>> CreateEmailWithAccount(int emailAct, string key)
		{
			List<string> emailIds = new List<string>();

			for (int i = 0; i < emailAct; i++)
			{
				string emailId = null;
				JObject account = new JObject();
				account["name"] = "AFN" + RandomString(3);
				JObject emailRecord = new JObject();
				emailRecord["description"] = "Email" + RandomString(3);
				emailRecord["subject"] = "email" + DateTime.UtcNow.Ticks + "_" + key;
				emailRecord["regardingobjectid_account_email"] = account;
				emailId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync<JObject>(HttpMethod.Post, EntityLogicalName.emails, emailRecord);
				emailIds.Add(emailId);
			}

			return emailIds;
		}

		/// <summary>
		/// Create multiple accounts with primary contact id.
		/// </summary>
		/// <param name="countAcc">no of accounts</param>
		/// <param name="key">Identifier</param>
		/// <returns>List of accountIds</returns>
		private async Task<List<string>> CreateAccountsWithPrimaryContact(int countAcc, string key)
		{
			List<string> accountIds = new List<string>();

			for (int i = 0; i < countAcc; i++)
			{
				string accountId = null;
				JObject account = new JObject();
				JObject contact = new JObject();
				account["name"] = "act" + DateTime.UtcNow.Ticks + "_" + key;
				contact["lastname"] = "cont" + DateTime.UtcNow.Ticks + "_" + key;
				contact["emailaddress1"] = account["name"] + "@microsoft.com";
				contact["telephone1"] = account["name"];
				account["primarycontactid"] = contact;
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync<JObject>(HttpMethod.Post, EntityLogicalName.accounts, account);
				accountIds.Add(accountId);
			}

			return accountIds;
		}

		/// <summary>
		/// Create multiple accounts with multiple contacts
		/// </summary>
		/// <param name="countAcc"> no of accounts</param>
		/// <param name="countCont">no of contacts</param>
		/// <param name="key"></param>
		/// <returns>list of accountIds</returns>
		private async Task<List<string>> CreateAccountsWithContacts(int countAcc, int countCont, string key)
		{
			List<string> accountIds = new List<string>();

			for (int i = 0; i < countAcc; i++)
			{
				string accountId = null;
				JObject account = new JObject();
				JArray contacts = null;
				account["name"] = "act" + DateTime.UtcNow.Ticks + "_" + key;
				for (int j = 0; j < countCont; j++)
				{
					contacts = new JArray();
					JObject contact = new JObject();
					contact["lastname"] = "cont" + DateTime.UtcNow.Ticks + "_" + key;
					contacts.Add(contact);
				}

				account["contact_customer_accounts"] = contacts;
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync<JObject>(HttpMethod.Post, EntityLogicalName.accounts, account);
				accountIds.Add(accountId);
			}

			return accountIds;
		}

		/// <summary>
		/// Checks if the context URL and build URL matches
		/// </summary>
		/// <param name="response">Response object</param>
		/// <param name="contextBuildQuery">Query</param>
		/// <returns>Url match</returns>
		public bool CompareContextUrl(JObject response, string contextBuildQuery)
		{
			string contextUrl = response["@odata.context"].ToString();
			string urlToVerify = this.httpRequestFixture.BuildUriRequest(contextBuildQuery).ToString();
			return contextUrl.Contains(urlToVerify) ? true : false;
		}

		/// <summary>
		/// Automated TC480180 - Verify that a Property Value can be addressed
		/// </summary>
		/// <remarks>
		/// 1 - Create contact.
		/// 2 - Add header ("odata-maxversion", "4.0").
		/// 2 - Fire a GET request to retrieve value of contact id.
		/// 3 - Verify value of contact id retrieved is equal to the id of contact created.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyRetrievePropertyValue()
		{
			string contactId = string.Empty;

			try
			{
				// Create Contact
				contactId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.contacts, "TC480180_", "contact");

				// Retrieve contact id value created above
				Dictionary<string, string> header = new Dictionary<string, string>();
				header.Add("odata-maxversion", "4.0");
				string retrievePath = "contacts(" + contactId + ")/contactid/$value";
				HttpResponseMessage response = await this.httpRequestFixture.ExecuteRequestForUrl(retrievePath, header);
				string responseContactId = response.Content.ReadAsStringAsync().Result;
				Assert.True(responseContactId == contactId);
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.contacts, contactId);
			}
		}

		/// <summary>
		/// Automated TC480132 - Verify that service returns 204 No Content if the property content is set to null
		/// </summary>
		/// <remarks>
		/// 1 - Create an account without giving description field.
		/// 2 - Fire a GET request to retrieve description of the account.
		/// 3 - Verify status code of the response should reurn 204 No Content.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyePropertyValueWithOutContent()
		{
			string accountId = string.Empty;

			try
			{
				// Create an account
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "TC480132_account");

				// Retrieve description of the account
				string retrievePath = "accounts(" + accountId + ")/description";
				HttpResponseMessage response = await this.httpRequestFixture.ExecuteRequestForUrl(retrievePath);
				Assert.True(response.StatusCode == HttpStatusCode.NoContent);
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// Automated TC480146 - OData:Verify that an entity is created and it retrieves the correct values
		/// </summary>
		/// <remarks>
		/// 1 - Create an account with 'microsoft' in name.
		/// 2 - Fire a GET request to retrieve accounts where name contains 'microsoft'.
		/// 3 - Verify created account id is in retreived accounts.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyNameContainsMicrosoft()
		{
			string accountId = string.Empty;

			try
			{
				// Create an account
				accountId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "Microsoft_TC480132");

				// Retrieve accounts where name contains 'Microsoft'
				string retrievePath = "accounts/?$filter=contains(name,'microsoft')";
				JObject accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				JArray accountsArray = JArray.FromObject(accounts["value"]);
				Assert.True(accountsArray.Where(item => (item["accountid"].ToString()) == accountId).Count() == 1, "Expected 1 item but returned zero or more");
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// Automated TC480129 - Select Name and  NumberOfEmployees of top 8 accounts
		/// </summary>
		/// <remarks>
		/// 1 - Create 8 accounts.
		/// 2 - Fire a GET request to retrieve name and number of employees of top 8 accounts.
		/// 3 - Verify only name and number of employees are present in response.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyTop8Accounts()
		{
			List<string> accountIds = new List<string>();
			try
			{
				// Create 8 accounts
				for (int i = 0; i < 8; i++)
				{
					JObject account = new JObject();
					account["name"] = "TC480129" + i;
					account["numberofemployees"] = 34;
					account["revenue"] = 458;
					string accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.accounts, account);
					accountIds.Add(accountId);
				}

				// Retrieve name and number of employees of top 8 accounts
				string retrievePath = "accounts?$select=name,numberofemployees&$top=8";
				JObject accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				JArray accountsArray = JArray.FromObject(accounts["value"]);
				Assert.True(accountsArray.Where(item => (item["revenue"]) != null).Count() == 0, "Expected 0 item but returned more");
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				foreach (string accountId in accountIds)
				{
					await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
				}
			}
		}

		/// <summary>
		/// Automated TC480140 - Get the name, number of employees  of the account and expand the related contacts but select only First Name, Last Name and Full Name
		/// </summary>
		/// <remarks>
		/// 1 - Create an account with number of employees.
		/// 2 - Create a contact
		/// 3 - Associate account and contact with "contact_customer_accounts"
		/// 2 - Fire a GET request to retrieve name and number of employees of account and firstname, lastname and fullname of contact.
		/// 3 - Verify name, number of employees of account and firstname, lastname and fullname of contact are retrieved successfully.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyAttributesOfRelatedContact()
		{
			string accountId = string.Empty, contactId = string.Empty;

			try
			{
				// Create an account
				JObject account = new JObject();
				string accountName = "TC480129_Account", contactFirstName = "TC480140_", contactLastName = "contact", contactFullName = contactFirstName + " " + contactLastName;
				int numberOfEmployees = 34;
				account["name"] = accountName;
				account["numberofemployees"] = numberOfEmployees;
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.accounts, account);

				// Create a contact
				contactId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.contacts, contactFirstName, contactLastName);

				// Associate account and contact
				var association = await this.httpRequestFixture.AssociateEntityAsync("accounts", accountId, "contacts", contactId, "contact_customer_accounts");

				// Retrieve Account name, number of employees , contact first name, last name and full name
				string retrievePath = "accounts(" + accountId + ")?$expand=contact_customer_accounts($select=firstname,lastname,fullname)&$select=name,numberofemployees";
				JObject accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				Assert.True(accounts["name"].ToString() == accountName && int.Parse(accounts["numberofemployees"].ToString()) == numberOfEmployees
								&& accounts["contact_customer_accounts"][0]["firstname"].ToString() == contactFirstName
								&& accounts["contact_customer_accounts"][0]["lastname"].ToString() == contactLastName
								&& accounts["contact_customer_accounts"][0]["fullname"].ToString() == contactFullName);
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.contacts, contactId);
			}
		}

		/// TC480187 ABNF: 2 URL Components - resource path 
		/// </summary>
		/// <remarks>
		/// 1.Create 5 contacts
		/// 2.Create an account associated with the contact
		/// 3.get the contact details using Nav property contact_customer_accounts
		/// 4.Verify if the count is matched with created no of contacts
		/// </remarks>
		/// <returns>Task object</returns>
		[Fact]
		public async void VerifyURLComponentsResourcePath()
		{
			string accountId = string.Empty;
			try
			{
				// Json array to create multiple contact records
				JArray contacts = new JArray();

				// A Json Object used to create account record
				JObject account = new JObject();
				for (int i = 0; i < 5; i++)
				{
					string firstName = "Contact" + this.RandomString(1);
					string lastName = this.RandomString(3);

					// A Json Object used to create contact record
					JObject contact = new JObject();
					contact["lastname"] = lastName;
					contact["firstname"] = firstName;
					contacts.Add(contact);
				}

				account["name"] = "TAC" + this.RandomString(4);
				account["contact_customer_accounts"] = contacts;
				accountId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.accounts, account);
				int contactsCount = await this.httpRequestFixture.GetEntityRecordCount(string.Format("accounts({0})/contact_customer_accounts", accountId));
				Assert.True(contacts.Count > 0 && contactsCount == contacts.Count(), "Failure Reason:-'contact_customer_accounts' records retrieved Wrongly");
			}
			finally
			{
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
			}
		}

		/// <summary>
		/// Automated TC480127 - Select all contacts name telephone number ,and email addresses along with owning user
		/// </summary>
		/// <remarks>
		/// 1 - Create a contact.
		/// 2 - Fire a GET request to retrieve contact name telephone number ,and email addresses along with owning user.
		/// 3 - Verify contact name telephone number ,and email addresses along with owning user are retrieved successfully.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyNameTelephoneEmailAddress()
		{
			string contactId = string.Empty;

			try
			{
				// Create a contact
				JObject contact = new JObject();
				contact["firstname"] = "TC480127_";
				contact["lastname"] = "contact";
				contact["telephone1"] = "9999999999";
				contact["emailaddress1"] = "email@mail.com";
				contact["address1_city"] = "city";
				contactId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.contacts, contact);

				// Retrieve contact name telephone number ,and email addresses along with owning user
				string retrievePath = "contacts?$select=fullname,telephone1,emailaddress1,owninguser";
				JObject contactAttributes = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				JArray accountsArray = JArray.FromObject(contactAttributes["value"]);
				Assert.True(accountsArray.Where(item => (item["address1_city"]) != null).Count() == 0, "Expected 0 item but returned more");
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.contacts, contactId);
			}
		}

		/// <summary>
		/// Automated TC480161 - ABNF:5.1.1.1.5 Less Than
		/// </summary>
		/// <remarks>
		/// 1 - Create two accounts.
		/// 2 - Fire a GET request to retrieve accounts name less than.
		/// 3 - Verify accounts retrieved are with name less than name given in query.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public void VerifyLessThan()
		{
			VerifyComparator("lt");
		}

		/// <summary>
		/// Automated TC480162 - ABNF:5.1.1.1.6 Less Than or Equal
		/// </summary>
		/// <remarks>
		/// 1 - Create two accounts.
		/// 2 - Fire a GET request to retrieve accounts name less than or equal.
		/// 3 - Verify accounts retrieved are with name less than or equal to the name given in query.
		/// </remarks>
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public void VerifyLessThanOrEqual()
		{
			VerifyComparator("le");
		}

		/// <summary>
		/// Automated TC480160 - ABNF:5.1.1.1.4 Greater Than or Equal
		/// </summary>
		/// <remarks>
		/// 1 - Create two accounts.
		/// 2 - Fire a GET request to retrieve accounts name less than.
		/// 3 - Verify accounts retrieved are with name greater than or equal to the name given in query.
		/// </remarks>
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public void VerifyGreaterThanOrEqual()
		{
			VerifyComparator("ge");
		}

		/// <summary>
		/// Automated TC480159 - ABNF:5.1.1.1.3 Greater Than
		/// </summary>
		/// <remarks>
		/// 1 - Create two accounts.
		/// 2 - Fire a GET request to retrieve accounts name less than.
		/// 3 - Verify accounts retrieved are with name greater than name given in query.
		/// </remarks>
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public void VerifyGreaterThan()
		{
			VerifyComparator("gt");
		}

		/// <summary>
		/// Automated TC480135 - [Scorecard] Get all emails in the org
		/// </summary>
		/// <remarks>
		/// 1 - Fire a GET request to retrieve emails with count=true.
		/// 2 - Verify '@odata.count' is present in the response
		/// 3 - Fire a GET request to retrieve emails with count=false.
		/// 4 - Verify '@odata.count' is not present in the response
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyEmailWithCount()
		{
			// Retrieve emails with count=true
			string retrievePath = "emails?$count=true";
			JObject emails = await this.httpRequestFixture.GetEntityRecords(retrievePath);
			Assert.True(emails.ContainsKey("@odata.count"));

			// Retrieve emails with count=false
			retrievePath = "emails?$count=false";
			emails = await this.httpRequestFixture.GetEntityRecords(retrievePath);
			Assert.False(emails.ContainsKey("@odata.count"));
		}

		/// <summary>
		/// Automated TC480184 - ABNF: Resource Path - Entity Set
		/// </summary>
		/// <remarks>
		/// 1 - Fire a GET request to retrieve count of all accounts.
		/// 2 - Add 2 accounts
		/// 3 - Fire a GET request to retrieve count of all accounts.
		/// 4 - Verify count is increased by 2
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyAccountsEntitySet()
		{
			string accountOneId = string.Empty, accountTwoId = string.Empty;

			try
			{
				int count = await this.httpRequestFixture.GetEntityRecordCount("accounts");

				// Create 2 accounts
				accountOneId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "TC480184_account1");
				accountTwoId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, "TC480184_account2");

				// Verify count after creating 2 accounts
				int nextCount = await this.httpRequestFixture.GetEntityRecordCount("accounts");
				Assert.True(nextCount == count + 2);
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountOneId);
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountTwoId);
			}
		}

		/// <summary>
		/// Automated TC480164 - ABNF:5.1.4 OrderBy asc
		/// </summary>
		/// <remarks>
		/// 1 - Create 3 accounts.
		/// 2 - Fire a GET request to retrieve accounts order by name.
		/// 3 - Verify accounts retrieved are in order.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyOrderByAsc()
		{
			List<string> accountIds = new List<string>();

			try
			{
				// create 3 accounts
				string accountName = "accounts_" + DateTime.UtcNow.ToString("yyMMddHHmmss");
				string accountOneId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountName + "1");
				string accountTwoId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountName + "2");
				string accountThreeId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountName + "3");

				// Retrieve accounts order by asc
				accountIds.Add(accountOneId);
				accountIds.Add(accountTwoId);
				accountIds.Add(accountThreeId);
				string retrievePath = "accounts?$filter=contains(name,'" + accountName + "')&$orderby=name asc";
				JObject accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				JArray accountsArray = JArray.FromObject(accounts["value"]);
				List<string> accountsRetrieved = (from account in accountsArray where accountsArray.HasValues select account["accountid"].ToString()).ToList();
				Assert.True(accountsRetrieved.SequenceEqual<string>(accountIds));
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				foreach (string accountId in accountIds)
				{
					await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
				}
			}
		}

		/// <summary>
		/// Automated TC480126 - [Scorecard] Get number of all accounts that have revenue either null or  greater than 50000 dollar
		/// </summary>
		/// <remarks>
		/// 1 - Create 2 accounts.
		/// 2 - Fire a GET request to retrieve accounts with revenue equals null or revenue greater than 50000.
		/// 3 - Verify accounts retrieved are with revenue greater than 50000.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyRevenueValue()
		{
			string accountOneId = string.Empty, accountTwoId = string.Empty;

			try
			{
				// Create two accounts one with revenue and other without revenue
				string accountName = "TC480126_account";
				JObject accountOne = new JObject();
				accountOne["name"] = accountName + DateTime.UtcNow.ToString("yyMMddHHmmss");
				accountOne["revenue"] = 56000;
				accountOneId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.accounts, accountOne);

				accountTwoId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountName);

				// Retrieve accounts that have revenue either null or  greater than 50000 dollar
				string retrievePath = "accounts?$count=true&$filter=((revenue gt 50000) or (revenue eq null)) and contains(name,'" + accountName + "')";
				JObject accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				JArray accountsArray = JArray.FromObject(accounts["value"]);
				Assert.True(accountsArray.Where(item => (item["accountid"].ToString()) == accountOneId || (item["accountid"].ToString()) == accountTwoId).Count() == 2, "Expected 0 items but returned more");
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountOneId);
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountTwoId);
			}
		}

		/// <summary>
		/// Automated TC480130 - Retrive account  order by NumberOfEmployees and  name
		/// </summary>
		/// <remarks>
		/// 1 - Create 5 accounts.
		/// Scenario 1:If Number of Emaployees is null for some accounts, those accounts appear at the top in result.
		/// 1 - Retrieve accounts order by number of employees and name in descending order.
		/// 2 - Verify If Number of Emaployees is null for some accounts, those accounts appear at the top in result.
		/// Scenario 2:If Number of Emaployees is null for some accounts, those accounts appear at the bottom in result.
		/// 1 - Retrieve accounts order by number of employees in descending order and name.
		/// 2 - If Number of Emaployees is null for some accounts, those accounts appear at the bottom in result.
		/// </remarks>
		/// <returns></returns>
		[Fact]
		public async void VerifyOrderByNumberOfEmployees()
		{
			List<string> accountIds = new List<string>();

			try
			{
				string accountName = "account_" + DateTime.UtcNow.ToString("yyMMddHHmmss");
				// Create 2 accounts
				string accountOneId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountName + "1");
				string accountTwoId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountName + "2");

				// Create 3 accounts with number of employees
				JObject accountThree = new JObject();
				accountThree["name"] = accountName + "3";
				accountThree["numberofemployees"] = 200;
				string accountThreeId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.accounts, accountThree);

				JObject accountFour = new JObject();
				accountFour["name"] = accountName + "4";
				accountFour["numberofemployees"] = 500;
				string accountFourId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.accounts, accountFour);

				JObject accountFive = new JObject();
				accountFive["name"] = accountName + "5";
				accountFive["numberofemployees"] = 800;
				string accountFiveId = await this.httpRequestFixture.CreateOrUpdateEntityRecordAsync(HttpMethod.Post, EntityLogicalName.accounts, accountFive);

				// If Number of Emaployees is null for some accounts, those accounts appear at the top in result
				accountIds.Add(accountTwoId);
				accountIds.Add(accountOneId);
				accountIds.Add(accountThreeId);
				accountIds.Add(accountFourId);
				accountIds.Add(accountFiveId);
				string retrievePath = "accounts?$select=name,numberofemployees&$filter=contains(name,'" + accountName + "')&$orderby=numberofemployees,name desc";
				JObject accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				JArray accountsArray = JArray.FromObject(accounts["value"]);
				List<string> accountsRetrieved = (from account in accountsArray where accountsArray.HasValues select account["accountid"].ToString()).ToList();
				Assert.True(accountsRetrieved.SequenceEqual<string>(accountIds));

				// If Number of Emaployees is null for some accounts, those accounts appear at the bottom in result
				accountIds.Reverse();
				retrievePath = "accounts?$select=name,numberofemployees&$filter=contains(name,'" + accountName + "')&$orderby=numberofemployees desc,name";
				accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				accountsArray = JArray.FromObject(accounts["value"]);
				accountsRetrieved = (from account in accountsArray where accountsArray.HasValues select account["accountid"].ToString()).ToList();
				Assert.True(accountsRetrieved.SequenceEqual<string>(accountIds));
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				foreach (string accountId in accountIds)
				{
					await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountId);
				}
			}
		}

		/// <summary>
		/// To verify operators less than, lesa than or equal, greater than, greater than or equal
		/// </summary>
		/// <param name="comparator">operators: le, lt, ge, gt</param>
		private async void VerifyComparator(string comparator)
		{
			string accountOneId = string.Empty, accountTwoId = string.Empty, accountThreeId = string.Empty;

			try
			{
				string accountOneName = "account_" + DateTime.UtcNow.ToString("yyMMddHHmmss");
				accountOneId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountOneName);
				string accountTwoName = "account_" + DateTime.UtcNow.ToString("yyMMddHHmmss");
				accountTwoId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountTwoName);
				string accountThreeName = "account_" + DateTime.UtcNow.ToString("yyMMddHHmmss");
				accountThreeId = await this.httpRequestFixture.CreateBasicEntityRecord(EntityLogicalName.accounts, accountThreeName);

				// Retrieve accounts
				string retrievePath = "accounts?$filter=name " + comparator + " '" + accountTwoName + "'";
				JObject accounts = await this.httpRequestFixture.GetEntityRecords(retrievePath);
				JArray accountsArray = JArray.FromObject(accounts["value"]);

				switch (comparator)
				{
					case "gt":
						Assert.True(accountsArray.Where(item => (item["accountid"].ToString()) == accountThreeId).Count() == 1);
						break;
					case "ge":
						Assert.True(accountsArray.Where(item => item["accountid"].ToString() == accountThreeId || item["accountid"].ToString() == accountTwoId).Count() == 2);
						break;
					case "lt":
						Assert.True(accountsArray.Where(item => (item["accountid"].ToString()) == accountOneId).Count() == 1);
						break;
					case "le":
						Assert.True(accountsArray.Where(item => item["accountid"].ToString() == accountOneId || item["accountid"].ToString() == accountTwoId).Count() == 2);
						break;
				}
			}
			catch(Exception ex)
			{
				Assert.True(false, "Failed to execute the test case: " + ex.Message);
			}
			finally
			{
				// To cleanup
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountOneId);
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountTwoId);
				await this.httpRequestFixture.DeleteEntityRecordById(EntityLogicalName.accounts, accountThreeId);
			}
		}

		/// Generate random alphaneumeric string
		/// </summary>
		/// <param name="length">length of string</param>
		/// <returns>alphaneumeric string</returns>
		public string RandomString(int length)
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			return new string(Enumerable.Repeat(chars, length)
				.Select(s => s[random.Next(s.Length)]).ToArray());
		}
	}
}