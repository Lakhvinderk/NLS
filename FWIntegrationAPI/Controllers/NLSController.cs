using FWIntegrationAPI.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using RestSharp;
using Swashbuckle.AspNetCore.Annotations;
using System.Reflection;
using System.Text;

namespace FWIntegrationAPI.Controllers
{
    [Route("api/")]
    [ApiController]
    public class NLSController : ControllerBase
    {
        string basicLLIUri = "https://api.lloydslistintelligence.com/v1/";

        public NLSController()
        {
        }
        /// <summary>
        /// Generate an authentication token
        /// </summary>
        [HttpPost]
        [Route("GetAuthToken")]
        public async Task<AuthTokenResponse> GetAuthToken([FromBody] AuthTokenRequestModel Authrequest)
        {
            try
            {
                if (Authrequest == null)
                {
                    return new AuthTokenResponse
                    {
                        Message = "Request body is empty",
                        Token = null
                    };
                }

                var client = new RestClient(basicLLIUri + "tokenprovider");
                var request = new RestRequest("", Method.Post);

                request.AddJsonBody(new
                {
                    username = Authrequest.Username,
                    password = Authrequest.Password
                });

                var response = await client.ExecuteAsync(request);
                JObject obj = JObject.Parse(response.Content);

                var payload = obj["Payload"]?.ToString();
                var message = obj["Message"]?.ToString();

                if (!string.IsNullOrEmpty(payload))
                {
                    return new AuthTokenResponse
                    {
                        Message = "Success",
                        Token = payload
                    };
                }

                return new AuthTokenResponse
                {
                    Message = message ?? "Authentication failed",
                    Token = null
                };
            }
            catch (Exception ex)
            {
                return new AuthTokenResponse
                {
                    Message = ex.Message,
                    Token = null
                };
            }
        }

        /// <summary>
        /// Get vessel calls information from external API
        /// </summary>
        /// <param name="vesselImo">The IMO number of the vessel e.g. 9515802,9410026,8210699,5355632</param>
        /// <param name="vesselName">Current name of the vessel. Additional functionality available, see 'Standard Functionality'. e.g. Queen Mary 2</param>
        /// <param name="vesselMmsi">MMSI number currently used by the vessel. e.g. 310627000</param>
        /// <param name="classb">If supplied as ‘true’, known class B vessels will be included in the API response.</param>
        /// <param name="dateRange">If no dateRange supplied the API will return the previous 7 days history. Maximum dateRange allowed is 6 months. e.g. 2020-12-01 – 2020-12-31</param>
        /// <param name="includeBerths">If supplied as ‘true’ will include details of berth callings within the port (requires additional subscription).</param>
        /// <returns>Vessel call details in JSON format</returns>
        [HttpGet("GetVesselCalls")]
        public async Task<IActionResult> GetVesselCalls([FromQuery] string vesselImo, [FromHeader] string token, [FromQuery] string? vesselName, [FromQuery] string? vesselMmsi, [FromQuery] bool? classb, [FromQuery] string? dateRange, [FromQuery] bool? includeBerths)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var urlBuilder = new StringBuilder(basicLLIUri + "vesselcalls_v2?");

                    var queryParams = new List<string>();

                    if (!string.IsNullOrEmpty(vesselImo))
                        queryParams.Add($"vesselImo={Uri.EscapeDataString(vesselImo)}");

                    if (!string.IsNullOrEmpty(vesselName))
                        queryParams.Add($"vesselName={Uri.EscapeDataString(vesselName)}");

                    if (!string.IsNullOrEmpty(vesselMmsi))
                        queryParams.Add($"vesselMmsi={Uri.EscapeDataString(vesselMmsi)}");

                    if (classb.HasValue)
                        queryParams.Add($"classb={classb.Value.ToString().ToLower()}");

                    if (!string.IsNullOrEmpty(dateRange))
                        queryParams.Add($"dateRange={Uri.EscapeDataString(dateRange)}");

                    if (includeBerths.HasValue)
                        queryParams.Add($"includeBerths={includeBerths.Value.ToString().ToLower()}");

                    // Join all parameters with &
                    var url = urlBuilder.ToString() + string.Join("&", queryParams);

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Authorization", token);
                    request.Headers.Add("Accept", "application/json");

                    var response = await httpClient.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        try
                        {
                            var errorJson = JToken.Parse(content);
                            return StatusCode((int)response.StatusCode, errorJson);
                        }
                        catch
                        {
                            return StatusCode((int)response.StatusCode, new
                            {
                                IsSuccess = false,
                                StatusCode = (int)response.StatusCode,
                                Message = content,
                                Error = content
                            });
                        }
                    }

                    return Content(content, "application/json");

                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { IsSuccess = false, Errors = new[] { ex.Message } });
            }
        }
        /// <summary>
        /// Advanced Compliance risk ratings for one or more vessels, including risks flagged and their details across AIS Gaps, Loitering, AIS Manipulation, Flag, Ownership, Ship to Ship Transfer and movements (available by supplying either VesselId, VesselImo, VesselName, vesselMmsi)
        /// </summary>
        /// <param name="vesselImo">The IMO number of the vessel e.g. 9515802,9410026,8210699,5355632</param>
        /// <param name="vesselName">Current name of the vessel. Additional functionality available, see 'Standard Functionality'. e.g. Queen Mary 2</param>
        /// <param name="vesselMmsi">MMSI number currently used by the vessel. e.g. 310627000</param>
        /// <param name="riskType">Type of individual compliance risk that API endpoint output should fetch. For allowed values see below. Default All. e.g. flag,sts</param>
        /// <param name="vesselComplianceRiskScore">Filter results by overall vessel compliance risk score. For allowed values see below. e.g. High,Medium</param>
        /// <param name="vesselAisGapRating">Filter results by AIS gap risk rating. For allowed values see below. e.g. High</param>
        /// <returns>Vessel call details in JSON format</returns>

        [HttpGet("GetAdvancedComplianceRisk")]
        public async Task<IActionResult> GetAdvancedComplianceRisk([FromQuery] string vesselImo, [FromHeader] string token, [FromQuery] string? vesselName, [FromQuery] string? vesselMmsi, [FromQuery] string? riskType, [FromQuery] string? vesselComplianceRiskScore, [FromQuery] string? vesselAisGapRating)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var urlBuilder = new StringBuilder(basicLLIUri + "vesseladvancedcompliancerisk_v4?");

                    var queryParams = new List<string>();

                    if (!string.IsNullOrEmpty(vesselImo))
                        queryParams.Add($"vesselImo={Uri.EscapeDataString(vesselImo)}");

                    if (!string.IsNullOrEmpty(vesselName))
                        queryParams.Add($"vesselName={Uri.EscapeDataString(vesselName)}");

                    if (!string.IsNullOrEmpty(vesselMmsi))
                        queryParams.Add($"vesselMmsi={Uri.EscapeDataString(vesselMmsi)}");

                    if (!string.IsNullOrEmpty(riskType))
                        queryParams.Add($"riskType={Uri.EscapeDataString(riskType)}");

                    if (!string.IsNullOrEmpty(vesselComplianceRiskScore))
                        queryParams.Add($"vesselComplianceRiskScore={Uri.EscapeDataString(vesselComplianceRiskScore)}");

                    if (!string.IsNullOrEmpty(vesselAisGapRating))
                        queryParams.Add($"vesselAisGapRating={Uri.EscapeDataString(vesselAisGapRating)}");

                    var url = urlBuilder.ToString() + string.Join("&", queryParams);

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Authorization", token);
                    request.Headers.Add("Accept", "application/json");

                    var response = await httpClient.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        try
                        {
                            var errorJson = JToken.Parse(content);
                            return StatusCode((int)response.StatusCode, errorJson);
                        }
                        catch
                        {
                            return StatusCode((int)response.StatusCode, new
                            {
                                IsSuccess = false,
                                StatusCode = (int)response.StatusCode,
                                Message = content,
                                Error = content
                            });
                        }
                    }

                    // ✅ SUCCESS RESPONSE FROM API
                    return Content(content, "application/json");
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { IsSuccess = false, Errors = new[] { ex.Message } });
            }
        }
    }
}