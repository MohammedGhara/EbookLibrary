using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Threading.Tasks;

namespace OnlineLibrary.Controllers
{

    [Route("Order")]
    public class OrderController : Controller
    {
        private readonly IConfiguration _configuration;

        public OrderController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        [HttpPost("CreatePaymentIntent")]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] PaymentRequestDto request)
        {
            // Convert dollars to cents for Stripe
            long amountInCents = (long)(request.Amount * 100);

            // Your Stripe Secret Key
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = amountInCents,
                Currency = "usd",
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
            });

            // Return the clientSecret so the client can confirm the payment
            return Ok(new { clientSecret = paymentIntent.ClientSecret });
        }
    }

    // PaymentRequestDto
    public class PaymentRequestDto
    {
        public decimal Amount { get; set; }
    }
}