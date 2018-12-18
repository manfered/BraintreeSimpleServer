using Braintree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace BraintreeSimpleServer.Controllers
{
    public class HomeController : Controller
    {
        // by signing up in Braintree sandbox you will get these items
        private const string _merchantID = "43q642rmxwxjj2b5";
        private const string _publicKey = "dr2wvyghh3t7k5tk";
        private const string _privateKey = "b3a597c2fd4ec169f200326db2f7b81d";

        private BraintreeGateway gateway;

        public static readonly TransactionStatus[] transactionSuccessStatuses = {
                                                                                    TransactionStatus.AUTHORIZED,
                                                                                    TransactionStatus.AUTHORIZING,
                                                                                    TransactionStatus.SETTLED,
                                                                                    TransactionStatus.SETTLING,
                                                                                    TransactionStatus.SETTLEMENT_CONFIRMED,
                                                                                    TransactionStatus.SETTLEMENT_PENDING,
                                                                                    TransactionStatus.SUBMITTED_FOR_SETTLEMENT
                                                                                };

        public HomeController()
        {
            gateway = CreateGateway();
        }

        private static BraintreeGateway CreateGateway()
        {
            return new BraintreeGateway
            {
                Environment = Braintree.Environment.SANDBOX,
                MerchantId = _merchantID,
                PublicKey = _publicKey,
                PrivateKey = _privateKey
            };
        }


        public ActionResult Index()
        {
            return View();
        }

        public ActionResult SubscribeIndex()
        {
            return View();
        }

        public async Task<ActionResult> ClientToken()
        {
            var clientToken = await gateway.ClientToken.GenerateAsync(
                new ClientTokenRequest
                {
                    // CustomerId = aCustomerId  // CustomerId = A string value representing an existing customer in your Vault.
                }
            );

            return Json(new { data = clientToken }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult CreatePurchase(FormCollection collection)
        {
            string nonceFromTheClient = collection["payment_method_nonce"];
            // Use payment method nonce here

            // we have to check if the user has Braintree customer_id or not
            // for test usages we create 
            var customer_id = CreateCustomer(1);


            // for test usage create payment method
            var paymentMethodToken = CreatePaymentMethod(customer_id, nonceFromTheClient);

            // for test usage find customer's cards
            var paymentMethodsList = FindCustomerPaymentMethod(customer_id);

            // for test usage finding card info
            FindPaymentMethodCardInfo(paymentMethodToken);

            var request = new TransactionRequest
            {
                Amount = 10.00M,
                PaymentMethodNonce = nonceFromTheClient,
                Options = new TransactionOptionsRequest
                {
                    SubmitForSettlement = true
                }
            };

            Result<Transaction> result = gateway.Transaction.Sale(request);

            if (result.IsSuccess())
            {
                Transaction transaction = result.Target;
                TransactionStatus status = transaction.Status;

                PaymentInstrumentType paymentInstrumentType = transaction.PaymentInstrumentType;

                // important to have in order to void or refund
                string transaction_id = transaction.Id;
            }
            else
            {
                ValidationErrors errors = result.Errors;
                Transaction transaction = result.Transaction;

                if (transaction.Status == TransactionStatus.PROCESSOR_DECLINED)
                {
                    ProcessorResponseType processorResponseType = transaction.ProcessorResponseType;
                    // e.g. "soft_declined"
                    string processorResponseCode = transaction.ProcessorResponseCode;
                    // e.g. "2001"
                    string ProcessorResponseText = transaction.ProcessorResponseText;
                    // e.g. "Insufficient Funds"
                    string additionalProcessorResponse = transaction.AdditionalProcessorResponse;
                }

                if (transaction.Status == TransactionStatus.SETTLEMENT_DECLINED)
                {
                    string processorSettlementResponseCode = transaction.ProcessorSettlementResponseCode;
                    // e.g. "4001"
                    string ProcessorSettlementResponseText = transaction.ProcessorSettlementResponseText;
                    // e.g. "Settlement Declined"
                }

                if (transaction.Status == TransactionStatus.GATEWAY_REJECTED)
                {
                    string error_reason = "Gateway rejected.";
                    TransactionGatewayRejectionReason transactionGatewayRejectionReason = transaction.GatewayRejectionReason;
                    // e.g. "avs"
                }
            }

            return Content(result.ToString());
        }

        [HttpPost]
        public ActionResult CreateSubscription()
        {
            // in order to provive PaymentMethodToken we have to have customer object
            // user needs to have a customer_id in braintree
            // if user already has a customer_id we find it from Braintree
            // if no we have to create a customer for the user

            // check in db if the user has a customer_id
            Customer userCustomer;
            if (true)
            {
                string customerID = "233596121"; // example
                userCustomer = FindCustomer(customerID);
            }
            else
            {
                // create a customer first
                int userId = 1; // example 
                string customerID = CreateCustomer(userId);
                userCustomer = FindCustomer(customerID);
            }


            // Before creating subscriptions, you must create a plan in the Control Panel.
            // Plans can't be created, updated, or deleted via the API.
            // However, you can retrieve existing plans via the API.

            var plansList = GetPlans();

            // finding the rightPlan
            var planID = plansList.FirstOrDefault(p => p.Name == "Plan_service1").Id; // example "silver_plan"

            var request = new SubscriptionRequest
            {
                PaymentMethodToken = GetPaymentMethodToken(userCustomer),
                PlanId = planID
            };

            Result<Subscription> result = gateway.Subscription.Create(request);

            if (result.IsSuccess())
            {
                Subscription subscription = result.Target;
                SubscriptionStatus status = subscription.Status;


                // important to have in order to cancel
                string subscription_id = subscription.Id;
            }
            else
            {
                ValidationErrors errors = result.Errors;
                Subscription subscription = result.Subscription;

                // if the subscribtion declined
                // there is an ability to manually retry the charge

                // example
                Result<Transaction> retryResult = gateway.Subscription.RetryCharge(
                    subscription.Id,
                    24.00M, // example
                    true
                );
                if (retryResult.IsSuccess())
                {
                    // true
                }
            }

            return Content(result.ToString());
        }

        public bool CancelSubscribtion(string subscription_id)
        {
            var result = gateway.Subscription.Cancel(subscription_id);
            if (result.IsSuccess())
            {
                return true;
            }
            return false;
        }

        public Subscription FindSubscription(string subscription_id)
        {
            return gateway.Subscription.Find("a_subscription_id");
        }

        private string CreateCustomer(int userId)
        {
            // find the user information id db
            // and provide here in order to create a customer
            var request = new CustomerRequest
            {
                FirstName = "Farzad",
                LastName = "Seifi",
                //Company = "Jones Co.",
                Email = "farzad.seifi@gmail.com",
                //Fax = "419-555-1234",
                Phone = "587-429-3959",
                //Website = "http://example.com"
            };
            Result<Customer> result = gateway.Customer.Create(request);

            string customerId = string.Empty;
            if (result.IsSuccess())
            {
                customerId = result.Target.Id;
                // e.g. 594019

                // store the customer_id in db
            }
            else
            {

            }

            return customerId;
        }

        private Customer FindCustomer(string customer_id)
        {
            return gateway.Customer.Find(customer_id);
        }

        public string CreatePaymentMethod(string customer_id, string NonceFromTheClient)
        {
            var request = new PaymentMethodRequest
            {
                CustomerId = customer_id,
                PaymentMethodNonce = NonceFromTheClient,
                Options = new PaymentMethodOptionsRequest // verifying all cards before they are stored in your Vault
                {
                    VerifyCard = true
                }
            };

            Result<PaymentMethod> result = gateway.PaymentMethod.Create(request);

            if (result.IsSuccess())
            {
                return result.Target.Token;
            }
            return result.Errors.ToString();
        }

        public string UpdatePaymentMethod(string PaymentMethodToken, string NonceFromTheClient)
        {
            var updateRequest = new PaymentMethodRequest
            {
                //BillingAddress = new PaymentMethodAddressRequest
                //{
                //    StreetAddress = "100 Maple Lane",
                //    Options = new PaymentMethodAddressOptionsRequest
                //    {
                //        UpdateExisting = true
                //    }
                //},
                PaymentMethodNonce = NonceFromTheClient
            };

            Result<PaymentMethod> result = gateway.PaymentMethod.Update(PaymentMethodToken, updateRequest);

            if (result.IsSuccess())
            {
                return result.Target.Token;
            }
            return result.Errors.ToString();
        }

        public PaymentMethod FindPaymentMethod(string PaymentMethodToken)
        {
            return gateway.PaymentMethod.Find(PaymentMethodToken);
        }

        public void FindPaymentMethodCardInfo(string PaymentMethodToken)
        {
            try
            {
                PaymentMethod paymentMethod = gateway.PaymentMethod.Find(PaymentMethodToken);
                paymentMethod.GetType();

                CreditCard creditCard = (CreditCard)paymentMethod;
                
                var returnObject = new
                {
                    LastFour = creditCard.LastFour, // 1234
                    ExpirationYear = creditCard.ExpirationYear,
                    ExpirationMonth = creditCard.ExpirationMonth,
                    MaskedNumber = creditCard.MaskedNumber,
                    CardTypeName = creditCard.CardType.ToString()
                };
            }
            catch (Exception ex)
            {

                throw;
            }
            
        }

        public void MakePaymentMethodDefault(string PaymentMethodToken)
        {
            var updateRequest = new PaymentMethodRequest
            {
                Options = new PaymentMethodOptionsRequest
                {
                    MakeDefault = true
                }
            };

            Result<PaymentMethod> result = gateway.PaymentMethod.Update(PaymentMethodToken, updateRequest);
        }

        public bool DeletePaymentMethod(string PaymentMethodToken)
        {
            var result = gateway.PaymentMethod.Delete(PaymentMethodToken);

            return result.IsSuccess();
        }

        private static string GetPaymentMethodToken(Customer userCustomer)
        {
            return userCustomer.PaymentMethods.First().Token;
        }

        public void FindPaymentMethodNonceCardInfo(string PaymentMethodNonce)
        {
            try
            {
                PaymentMethodNonce paymentMethodNonce = gateway.PaymentMethodNonce.Find(PaymentMethodNonce);
                ThreeDSecureInfo info = paymentMethodNonce.ThreeDSecureInfo;
                if (info == null)
                {
                    return; // This means that the nonce was not 3D Secured
                }
                var returnObject = new
                {
                    cardType = paymentMethodNonce.Details.CardType,
                    last_four = paymentMethodNonce.Details.LastFour,
                    type = paymentMethodNonce.Type
                };

            }
            catch (Exception ex)
            {

            }

            //paymentMethodNonce.BinData

        }

        private PaymentMethod[] FindCustomerPaymentMethod(string customer_id)
        {
            Customer customer = gateway.Customer.Find(customer_id);
            return customer.PaymentMethods; // array of PaymentMethod instances
        }

        private List<Plan> GetPlans()
        {
            return gateway.Plan.All();
        }

        private bool FindTransactionSuccessStatus(string transaction_id)
        {
            Transaction transaction = gateway.Transaction.Find(transaction_id);

            if (transactionSuccessStatuses.Contains(transaction.Status))
            {
                // success
                return true;
            }
            // Transaction Failed
            // we can get the status by transaction.Status
            return false;
        }

        private bool FindTransactionSettledStatus(string transaction_id)
        {
            Transaction transaction = gateway.Transaction.Find(transaction_id);

            if (transaction.Status == TransactionStatus.SETTLED)
            {
                // SETTLED
                return true;
            }
            return false;
        }


        public ActionResult Refund(string transaction_id)
        {
            Transaction transaction = gateway.Transaction.Find(transaction_id);

            if (transaction.Status == TransactionStatus.SUBMITTED_FOR_SETTLEMENT || transaction.Status == TransactionStatus.AUTHORIZED)
            {
                // can void
                Result<Transaction> result = gateway.Transaction.Void(transaction_id);

                if (result.IsSuccess())
                {
                    // transaction successfully voided
                }
                else
                {
                    foreach (ValidationError error in result.Errors.DeepAll())
                    {
                        // Console.WriteLine(error.Message);
                    }
                }
            }
            else if (transaction.Status == TransactionStatus.SETTLED || transaction.Status == TransactionStatus.SETTLING)
            {
                // will have to refund it
                Result<Transaction> result = gateway.Transaction.Refund("the_transaction_id");
                if (result.IsSuccess())
                {
                    // transaction successfully refunded
                }
                else
                {
                    List<ValidationError> errors = result.Errors.DeepAll();
                }
            }
            else
            {
                // this example only expected one of the two above statuses
            }

            return Content(transaction.Status.ToString());
        }

        [HttpGet]
        public ActionResult CardInfo()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CardInfo(string Nonce)
        {
            FindPaymentMethodNonceCardInfo(Nonce);

            return View();
        }


    }
}