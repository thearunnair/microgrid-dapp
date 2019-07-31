//---------------------------------------------------------------------------------------------
// EVENTS

[DisplayName("transaction")]
public static event Action<string, string, BigInteger, BigInteger> Transfer;
[DisplayName("membership")]
public static event Action<string, string> Membership;
[DisplayName("process")]
public static event Action<string, string> Process;
[DisplayName("ballot")]
public static event Action<string, string, bool> Ballot;
[DisplayName("offer")]
public static event Action<string, string, BigInteger> Offer;
[DisplayName("change")]
public static event Action<string, string> Update;
[DisplayName("refund")]
public static event Action<string, BigInteger> Refund;


//---------------------------------------------------------------------------------------------
// GLOBAL VARIABLES

// Power limits of the distributed generation category defined by Brazilian law (from 0MW to 5MW).
public static int[] PowGenLimits() => new int[] {0, 5000000};

// The total number of power plant units.
public static BigInteger NumOfPP() => Storage.Get("NumOfPP").AsBigInteger();

// The total number of members.
public static BigInteger NumOfMemb() => Storage.Get("NumOfMemb").AsBigInteger();

// The total power supply at the group, i.e., sum of PP's capacity.
public static BigInteger TotalSupply() => Storage.Get("TotalSupply").AsBigInteger();

// The number of days to answer a referendum process.
private const byte timeframeRef = 30;         // Review the sum with uint (is the endtime right?) --PENDING--

// The time a given function is invoked.
private static uint StartTime() => Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp; // --PENDING--

// Token settings.
public static string Name() => "Sharing Electricity in Brazil";
public static string Symbol() => "SEB";
public static byte Decimals() => 3;                                                         // {0, 5000}
public static byte[] Owner() => ExecutionEngine.ExecutingScriptHash;                        // aka GetReceiver() -- this smart contract
public static string[] SupportedStandards() => new string[] { "NEP-5", "NEP-7", "NEP-10" };

// Member's dataset.
private static string[] profile => new string[] {"FullName", "Utility"};
private static string[] register => new string[] {"Quota", "Tokens"};

// New Power Plant crowdfunding settings (ICO of an NFT).
private const ulong factor = 1000;              // Review at PowerUP() last operations --PENDING-- 1kW =?= 1SEB
private const byte minOffer = 1;                // Review restriction because it was not used so far. --PENDING--
private const byte timeframeCrowd = 60;         // Review the sum with uint (is the endtime right?) --PENDING--

// The restrictive message to show up.
private static Exception Warning() => new InvalidOperationException("Only members can access this information. Join us!");

// Caller authenticity...
public static byte[] Caller() => ...;                                                       // --PENDING--

//---------------------------------------------------------------------------------------------
// THE MAIN INTERFACE

public static object Main ( string operation, params object[] args )
{
    if ( Runtime.Trigger == TriggerType.Verification )
    {
        if ( Member.Get() == null ) // --PENDING--
        {
            if (args.Length != 2) return false; // --PENDING--
            Member( caller, args[0], args[1], 100, 0 ); // --PENDING--
            return "New GGM blockchain initiated."; // --PENDING--
        }

        return false;
    }
    else if ( Runtime.Trigger == TriggerType.Application )
    {
        // General operation.
        if (operation == "admission")
        {
            if ( args.Length != 3 )
                throw new InvalidOperationException("Please provide the 3 arguments: your account address, full name and power utility name.");

            if ( !Runtime.CheckWitness((string)args[0]) ) // --PENDING-- aqui o args[0] deve ser byte[]...
                throw new InvalidOperationException("The admission can not be done on someone else's behalf.");

            if ( GetMemb((string)args[0], "FullName").Length != 0 )
                throw new InvalidOperationException("Thanks, you're already a member. We're glad to have you as part of the group!");
            
            return Admission( (string)args[0],   // invoker/caller address
                              (string)args[1],   // fullName
                              (string)args[2] ); // utility
        }
        
        // Partially restricted operation.
        if (operation == "summary")
        {
            if ( args.Length != 1 )
                throw new InvalidOperationException("Provide at least a member address or a PP ID.");

            if ( (GetMemb(caller, "FullName").Length == null) | (args[0][0] == "A") ) // definir o caller é foda! --PENDING-- posso usar o VerifySignature?
                throw Warning();

            return Summary( (string)args[0],     // Address/ID
                            (string)args[1] );   // option
        }

        // Restricted operations.
        if ( GetMemb(caller, "FullName").Length != null )
        {
            // Group operations.
            if (operation == "vote")
            {
                if ( args.Length != 3 )
                    throw new InvalidOperationException("Please provide the 3 arguments: the referendum id, your account address, and your vote.");

                if ( !Runtime.CheckWitness((string)args[0]) ) // --PENDING-- aqui o args[0] deve ser byte[]...
                    throw new InvalidOperationException("The vote can not be done on someone else's behalf.");

                if ( isLock((string)args[0]) )
                    throw new InvalidOperationException("The ballot has ended.");
                
                return Vote( (string)args[0],    // referendum id
                             (string)args[1],    // member address
                             (bool)args[2] );    // answer
            }

            if (operation == "bid")
            {
                if ( args.Length != 3 )
                    throw new InvalidOperationException("Please provide the 3 arguments: the ICO id, your account address, and your bid.");

                if ( !Runtime.CheckWitness((string)args[0]) ) // --PENDING-- aqui o args[0] deve ser byte[]...
                    throw new InvalidOperationException("The bid can not be done on someone else's behalf.");

                if ( (args[0][0] != "P") || (args[0].Length == null) )
                    throw new InvalidOperationException("Provide a valid ICO ID.");

                if ( (GetPP(args[0], "Utility")) != (GetMemb(args[1], "Utility")) )
                    throw new InvalidOperationException( "This member cannot profit from this power utility." );

                if ( args[2] <= 0 ) return false;
                    throw new InvalidOperationException("Stop being a jerk.");
                
                if ( isLock((string)args[0]) )
                    throw new InvalidOperationException("The campaign has ended.");

                return Bid( (string)args[0],     // ICO id == PP id
                            (string)args[1],     // member address
                            (bool)args[2] );     // bid value
            }

            if (operation == "trade")
            {
                if ( args.Length != 4 )
                    throw new InvalidOperationException("Please provide the 4 arguments: your account address, the address of who you are transaction to, the quota value, and the amount of tokens.");

                if ( !Runtime.CheckWitness((string)args[0]) ) // --PENDING-- aqui o args[0] deve ser byte[]...
                    throw new InvalidOperationException("Only the owner of an account can exchange her/his asset.");

                if ( GetMemb(args[1], "FullName").Length != null )
                    throw new InvalidOperationException("The address you are transaction to must be a member too.");

                if ( (args[1][0] != "A") || (args[1].Length == null) )
                    throw new InvalidOperationException("Provide a valid destiny address.");

                if ( (GetMemb(args[0], "Utility")) != (GetMemb(args[1], "Utility")) )
                    throw new InvalidOperationException( "Both members must belong to the same power utility cover area." );

                if ( (args[2] <= 0) & (args[3] <= 0) )
                    throw new InvalidOperationException("You're doing it wrong. To donate energy let ONLY the 4th argument empty. Otherwise, to donate tokens let ONLY the 3rd argument empty.");
                
                return Trade( (string)args[0],       // from address
                              (string)args[1],       // to address
                              (BigInteger)args[2],   // quota exchange
                              (BigInteger)args[3] ); // token price
            }

            if (operation == "power up")
            {
                if (args.Length != 4)
                    throw new InvalidOperationException("Please provide the 4 arguments: the PP capacity, the cost to build it up, the power utility name in which the PP will be installed, and the period to wait the new PP gets ready to operate.");

                if ( (args[3] == 0) || (args[3] < 30) )
                    throw new InvalidOperationException("The time to market must be a factual period.");

                return PowerUp( (BigInteger)args[0],   // capacity [MW]
                                (BigInteger)args[1],   // cost [R$]
                                (string)args[2],       // power utility name
                                (ushort)args[3] );     // time to market
            }

            if (operation == "change")
            {
                if (args.Length != 2)
                    throw new InvalidOperationException("Please provide 2 arguments only. The first one must be the identification of the member (address) or the PP (id). The second one must be an array. It can be either the options about the data that will be changed, or an empty array to request the delete of something.");
                
                if ( (args[1][0] in profile) & !(Runtime.CheckWitness(args[0])) )
                    throw new InvalidOperationException("Only the member can change its own personal data.");
                
                if ( (args[0][0] != "A") | args[0][0] != "P"  )
                    throw new InvalidOperationException("Provide a valid member address or PP ID.");
                
                
                // Can change a bid? Because args[0][0] == "P"                --PENDING--
                
                return Change( (string)args[0],     // member address or PP id
                               (object[])args[1] ); // array with desired values --PENDING-- test length
            }
            
            // Administrative operations.
            if (operation == "admission result")
            {
                if ( args.Length != 1 )
                    throw new InvalidOperationException("Please provide the admission process ID.");
                
                if ( StartTime() <= GetRef( (string)args[0], "endTime" ) )
                    throw new InvalidOperationException("There is no result yet.");
                
                return AdmissionResult( (string)args[0] ); // Referendum ID
            }
            
            if (operation == "change result")
            {
                if ( args.Length != 1 )
                    throw new InvalidOperationException("Please provide the change process ID.");
                
                if ( StartTime() <= GetRef( (string)args[0], "endTime" ) )
                    throw new InvalidOperationException("There is no result yet.");
                
                ChangeResult( (string)args[0] ); // Referendum ID
            }
        }

        throw Warning();
        // return false;
    }

    return false;
}


//---------------------------------------------------------------------------------------------
// GROUP FUNCTIONS - The restrictions are made on the 'Main'.

// To request to join the group.
public static string Admission( string address, string fullName, string utility )
{
    string id = Ref( "Membership request_", String.Concat( fullName, utility ) );
    Membership( address, "Request for admission." );
    
    return id;
}

// To get information about something.
public static object Summary( string key, string opt = "" )
{
    // If 'key' is an 'address' ==  member.
    if (key[0] == "A")
    {
        if ((opt == "") || (opt == "detailed"))
        {
            string[] brief = new string[] { GetMemb(key,"FullName"), GetMemb(key,"Utility"), GetMemb(key,"Quota"), GetMemb(key,"Tokens") };

            if (opt == "detailed")
            {
                string[] PowerPlantsByMember = GetContributeValue( key, listOfPPs() );
                return brief + PowerPlantsByMember; // wrong concatenation method --PENDING--
            }
            return brief;
        }
        return GetMemb(key,opt);
    }

    // If 'key' is an 'id' with prefix 'P' == power plant.
    else if (key[0] == "P")
    {
        // The PP's crowdfunding had succeed and the PP is operating.
        if ( GetPP(key,"TotMembers").Length != 0 )
        {
            if ( (opt == "") || (opt == "detailed") )
            {
                string[] brief = new string[] { GetPP(key,"Capacity"), GetPP(key,"Cost"), GetPP(key,"Utility"), GetPP(key,"TotMembers") };
    
                if (opt == "detailed")
                {
                    string[] MembersByPowerPlant = GetContributeValue( key, listOfMembers() );
                    return brief + MembersByPowerPlant; // wrong concatenation method --PENDING--
                }
                return brief;
            }
            return GetPP(key,opt);
        }
        
        // The PP's crowdfunding may be succeed or not and the PP is definitely not operating.
        else
        {
            if ( (opt == "") || (opt == "detailed") )
            {
                string[] brief = new string[] { GetCrowd(key,"StartTime"), GetCrowd(key,"EndTime"), GetCrowd(key,"TotalAmount"), GetCrowd(key,"Contributions"), GetCrowd(key,"Success") };
    
                if (opt == "detailed")
                {
                    string[][] PowerPlantBids = new string[][];
                    
                    for each member in Members() // to be implemented {[Member, quota]} ? HOW? --PENDING--
                    {
                        BigInteger bid = GetBid(key, member).AsBigInteger();
                        if ( bid != 0 ) PowerPlantBids.append( [member, bid] );
                    }
                    
                    return brief + PowerPlantBids; // wrong concatenation method --PENDING--
                }
                return brief;
            }
            return GetCrowd(key,opt); // sempre vai retornar byte[], a conversão final tem q ser feita de acordo com a opção escolhida para se ter o valor correto de número, texto ou boleano.
        }
    }

    // If 'key' is an 'id' with prefix 'R' == referendum process.
    else if (key[0] == "R")
    {
        if (opt == "")
        {
            return new string[] { GetRef(key,"Proposal"), GetRef(key,"Notes"), GetRef(key,"Cost"), GetRef(key,"Outcome") };
        }
        return GetRef(key,opt);
    }

    // Wrap-up the group information.
    else
    {
        return new string[] { PowGenLimits()[0], PowGenLimits()[1], NumOfPP(), NumOfMemb(), Name(), Symbol(), TotalSupply() };
    }
}

// To vote in a given ID process.
public static bool Vote( string id, string member, bool answer )
{
    // Increase the number of votes.
    BigInteger temp = GetRef(id,"NumOfVotes").AsBigInteger();
    UpRef(id, "NumOfVotes", temp++);

    if (answer)
    {
        // Increase the number of "trues".
        BigInteger temp = GetRef(id,"CountTrue").AsBigInteger();
        UpRef(id, "CountTrue", temp++);
    }

    // Publish the vote.
    Ballot(id, member, answer);

    return answer;
}

// To make a bid in a new PP crowdfunding process (ICO of a NFT).
public static bool Bid( string ICOid, string member, BigInteger bid )
{
    BigInteger target = GetPP(ICOid, "Cost").AsBigInteger();
    BigInteger funds = GetCrowd(ICOid, "TotalAmount").AsBigInteger();
    
    if ( bid > target - funds )
        throw new InvalidOperationException( "You offered more than the amount requested ({0}). Bid again!".format( target - funds ) );

    // WARNING!
    // All these steps are part of a crowdfunding process, not of a PP registration.
    
    // Increases the value gathered so far.
    UpCrowd(ICOid, "TotalAmount", funds + bid);
    
    // Increases the number of contributions.
    BigInteger temp = GetCrowd(ICOid, "Contributions").AsBigInteger();
    UpCrowd(ICOid, "Contributions", temp++);
    
    // Tracks bid by member for each ICOid.
    BigInteger previous = Storage.Get( String.Concat(ICOid, member) ).AsBigInteger();
    Storage.Put( String.Concat(ICOid, member), previous + bid );
    Offer(ICOid, member, bid);
    
    return true;
    
    // If the hole fund process succeed, the money bid must be converted to percentage (bid/cost),
    // so it will be possible to define the quota and the SEB a member has to gain.
    // It is made on PowerUp().
}

// To update something on the ledger.
private object Change( string key, params object[] opts )
{
    // If 'key' is an 'address' ==  member.
    if (key[0] == "A")
    {
        // Only the member can change its own personal data.
        // To UPDATE, the params must be ['profile option', 'value'].
        if ( opts[1] is string )
        {
            UpMemb(key, opts[0], opts[1]);
            Update("Profile data.", key);
            return true;
        }
        
        // Any member can request the change of registration data of other member.
        // To UPDATE, the params must be ['register option', 'value'].
        if ( opts[1] is BigInteger )
        {
            string id = Ref( "Change register_", String.Concat( key, opts[0] ) );
            Process( id, "Request the change of registration data of a member." );
            return id;
        }
        
        // Any member can request to delete another member.
        if ( opts.Length == 0 )
        {
            string id = Ref( "Delete member_", "Distribute the shares and delete the tokens." );
            Process( id, "Request to dismiss a member." );
            return id;
        }
    }
    
    // If 'key' is an 'id' with prefix 'P' == power plant.
    if (key[0] == "P")
    {
        if ( (opts.Length != 1) & !(opts[0] is string) )
            throw new InvalidOperationException("Provide a valid power utility name to be replaced by.");
        
        // Any member can request the change of the 'utility' a PP belongs to.
        if ( opts.Length != 0 )
        {
            string id = Ref( "Change utility_", String.Concat( key, opts[0] ) );
            Process( id, "Request the change of utility name of a PP." );
            return id;
        }

        // Any member can request to DELETE a PP.
        string id = Ref( "Delete PP_", String.Concat( key, opts[0] ) );
        Process( id, "Request to delete a PP." );
        return id;
        
        
    }
}

// The whole process to integrate a new PP on the group power generation.
private string PowerUp( BigInteger capacity, BigInteger cost, string utility, ushort timeToMarket )
{
    string id = Ref( "New PP request_", String.Concat( capacity.ToString(), utility, timeToMarket.ToString() ), cost );
    Process( id, "Request to add a new PP." );
    return id;
}

// To allow the transfer of shares/tokens from someone to someone else (transactive energy indeed).
// The 'fromAddress' will exchange an amount of shares with 'toAddress' by a defined token price,
// i.e., while 'fromAddress' sends shares to 'toAddress', the 'toAddress' sends tokens to 'fromAddress'.
private bool Trade( string fromAddress, string toAddress, BigInteger exchange, BigInteger price )
{
    BigInteger[] toWallet = new BigInteger[];
    BigInteger[] fromWallet = new BigInteger[];
    
    // register = {"Quota", "Tokens"}
    foreach (string data in register)
    {
        fromWallet.append( GetMemb(fromAddress, data).AsBigInteger() );
        toWallet.append( GetMemb(toAddress, data).AsBigInteger() );
    }
    
    if ( ( fromWallet[0] < exchange ) || ( toWallet[1] < price ) ) return false;
    
    UpMemb(fromAddress, register[0], fromWallet[0] - exchange);
    UpMemb(toAddress, register[0], toWallet[0] + exchange);
    
    UpMemb(toAddress, register[1], toWallet[1] - price);
    UpMemb(fromAddress, register[1], fromWallet[1] + price);
    
    Transfer(fromAddress, toAddress, exchange, price);
    return true;
}


//---------------------------------------------------------------------------------------------
// SYSTEM FUNCTIONS

// A new PP will just distribute tokens and shares after a crowdfunding process succeed.    // --PENDING-- verificar com o caso de deletar membro!
// All the exceptions were handle during the crowdfunding. It only needs to distribute the assets.
private static void Distribute( string toAddress, BigInteger quota, BigInteger tokens )
{
    BigInteger[] toWallet = new BigInteger[];

    // register = {"Quota", "Tokens"}
    foreach (string data in register)
    {
        toWallet.append( GetMemb(toAddress, data).AsBigInteger() );
    }
    
    UpMemb(toAddress, register[0], toWallet[0] + quota);
    UpMemb(toAddress, register[1], toWallet[1] + tokens);
    Transfer(null, toAddress, quota, tokens);
}

// To create a custom ID of a process based on its particular specifications.
private static string ID( object arg1, object arg2, object arg3, object arg4 )  // --PENDING--
{
    object[] listOfArgs = new object[4] {arg1, arg2, arg3, arg4};
    
    for (int n = 0; n < 5; n++)
    {
        if (!(listOfArgs[n] is string))
        {
            listOfArgs[n] = (char)listOfArgs[n];
        }
    }

    string temp1 = String.Concat(arg1, arg2);
    string temp2 = String.Concat(arg3, arg4);
    return String.Concat(temp1, temp2);
    // string to byte[]
    // str.AsByteArray();
}

// To properly store a boolean variable.
private static string Bool2Str( bool val )
{
    if (val) return "1";
    return "0";
}

// To properly read a boolean from storage.
private static bool Str2Bool( byte[] val )
{
    if (val.AsString() == "1") return true;
    return false;
}

// To filter the relationship of members and PPs.
private static string[] GetContributeValue(string lookForID, string[] listOfIDs)
{
    //
    string[] equivList = new string[];
    
    // Gets values by each ID registered on the contract storage space.
    if ( lookForID[0] == "P" )
    {
        // Gets members by a PP.
        foreach (string key in listOfIDs)
        {
            BigInteger GetBid(lookForID, key).AsBigInteger();
            if ( temp != 0 ) equivList.append(temp);
        }
    }
    else
    {
        // Gets PPs by a member.
        foreach (string key in listOfIDs)
        {
            BigInteger GetBid(key, lookForID).AsBigInteger();
            if ( temp != 0 ) equivList.append(temp);
        }
    }
    
    return equivList;
}

// To get the IDs of each PP.
private static string[] listOfPPs()
{
    string[] listPPs = new string[];
    
    foreach (int num in NumOfPP())
    {
        string PP = Storage.Get( String.Concat( "P", num.ToString() ) ); // --PENDING--
        listMembers.append(PP);
    }
    
    return listPPs;
}

// To get the address of each member.
private static string[] listOfMembers()
{
    string[] listMembers = new string[];
    
    foreach (int num in NumOfMemb())
    {
        string member = Storage.Get( String.Concat( "M", num.ToString() ) ); // --PENDING--
        listMembers.append(member);
    }
    
    return listMembers;
}

// Actualy, it restricts a given operation to happen based on a timeframe.
// It must happen during any Referendum (new member, new PP), when a CrowdFunding is raised or when an Update of some information is requested (kind of referendum?).

// All this steps must be provided on the first invoke of the function. So the contract call set the trigger to run in the future. -- WHOLE SHEET!

// Check if some operation on the contract is locked for a while.
/**
    * The contract implements a function that specifies a certain timestamp.
    * Before the specified time stated, no one is allowed to withdraw any assets from the contract.
    * Once the time stated is reached, the contract owners can then withdraw the assets.
    *
    * INPUTS
    *   timestamp = the lock time in the sample code, which is a Unix timestamp.
    *               You can calculate it yourself or use it: https://unixtime.51240.com/
    *   pubkey = insert the previous copy of the public key byte array
    *   signature = the private key?
    **/
private static bool isLock( string id )
{
    Header header = Blockchain.GetHeader(Blockchain.GetHeight());
    
    
    uint endTime = GetRef(id, "endTime");                      // provavelmente vai dar erro de conversão!
    
    if (header.Timestamp < endTime) return false;
    return true;
}


//---------------------------------------------------------------------------------------------
// ADMINISTRATIVE FUNCTIONS

// After a period of 'timeframeRef' days a member should invoke this function to state the referendum process.
// An offchain operation should handle this.

private static void AdmissionResult( string id )
{
    if ( Str2Bool( GetRef(id, "Outcome") ) )
    {
        // Add a new member after approval from group members.
        Member( address, fullName, utility, 0, 0 );
        Membership( address, "Welcome on board!" );
    }

    Membership( address, "Not approved yet." );
}

private static void ChangeResult( string id )
{
    string proposal = GetRef(id, "Proposal").AsString();
    
    if (proposal == "Change register_")
    {
        if ( Str2Bool( GetRef(id, "Outcome") ) )
        {
            Process(id, "Approved.");
            UpMemb(key, opts[0], opts[1]);
            Update("Registration data.", key);
        }
        
        Process(id, "Denied.");
    }
                
    if (proposal == "Delete member_")
    {
        if ( Str2Bool( GetRef(id, "Outcome") ) )
        {
            Process(id, "Approved.");
            BigInteger portion = GetMemb(key, "Quota").AsBigInteger();
            BigInteger give_out = portion/(NumOfMemb() - 1);
            
            foreach (string member in listOfMembers())
            {
                // In an infinitesimal period of time the group will be disbalanced
                // until the related member be completely deleted.
                // There is no side effect and it is better than iterate through each member.
                
                Distribute(member, give_out, 0);
            }
    
            DelMemb(key);
            Membership(key, "Goodbye.");
        }
    
        Process(id, "Denied.");
    }
    
    if (proposal == "Change utility_")
    {
        if ( Str2Bool( GetRef(id, "Outcome") ) )
        {
            Process(id, "Approved.");
            UpPP(key, opts[0]);
            Update("Belonging of.", key);
        }

        Process(id, "Denied.");
    }
        
    if (proposal == "Delete PP_")
    {
        if ( Str2Bool( GetRef(id, "Outcome") ) )
        {
            Process(id, "Approved.");
            DelPP(key);
            Update("Deletion of.", key);
        }

        Process(id, "Denied.");
    }
}

private static object PowerUpResult( string id, string PPid = null )   // --PENDING--
{
    string notes = GetRef(id, "Notes"); // --PENDING--
            
    // separa os termos em Notes!           // --PENDING--
            
            
    // STEP 1 - After a 'timeframeRef' waiting period.
    if (PPid == null)
    {
        // Adds or not a new PP after votes from group members.
        if ( Str2Bool( GetRef(id, "Outcome") ) )
        {
            BigInteger capacity = notes[0];
            BigInteger cost = GetRef(id, "Cost");
            string utility = notes[1];
            
            return PP(capacity, cost, utility);     // PPid
        }
        else
        {
            Process(id, "This PP was not approved yet. Let's wait a bit more.");
            return false;
        }
    }

    // STEP 2 - If a new PP has been approved, starts to raise money for it.
    if ( GetCrowd(PPid, "startTime").Length == 0 )
    {
        CrowdFunding(PPid);
        Process(PPid, "Shut up and give me money!");
        return true;
    }
    
    
    ushort timeToMarket = notes[2];
    uint endTime = GetCrowd(PPid, "endTime");
    uint operationDate = endtime + timeToMarket;
            
    
    // STEP 3 - After a 'timeframeCrowd' waiting period.
    if ( (StartTime() > endTime) & (StartTime() < operationDate) )
    {
        if (?contributions = ?target)
        {
            UpCrowd( PPid, true ); // Aqui é o único lugar em que isso está acontecendo? É aqui que isso deve ser definido?
        }
        
        // Gets a list of funders of the respective PP.
        string[] litsOfFunders = GetContributeValue( PPid, listOfMembers() );
    
        // If crowdfunding succeeds.
        if ( Str2Bool( GetCrowd( PPid, "Success") ) )
        {
            // Update the number of fund members database
            BigInteger numOfFundMemb = ...; // --PENDING--
            UpPP(id, "numOfFundMemb", numOfFundMemb);
            Process(id, "New power plant on the way.");
            return true;
        }
        else
        {
            foreach (string funder in litsOfFunders)
            {
                Refund(PPid, funder);
            }
            Process(id, "Fundraising has failed.");
            return false;
        }
    }
    
    // STEP 4 - After waiting for the time to market.
    if ( StartTime() > operationDate )
    {
        // When the PP starts to operate, it's time to distribute tokens and shares.
            
        // Increases the total power supply of the group.
        BigInteger capOfPP = GetPP(ICOid, "Capacity").AsBigInteger();
        BigInteger capOfGroup = TotalSupply() + capOfPP;
        Storage.Put("TotalSupply", capOfGroup);
    
        // The presence of the PP which accounts for on the group.
        BigInteger sharesOfPP = capOfPP/capOfGroup;
        
        foreach (string funder in litsOfFunders)
        {
            BigInteger grant = GetBid(ICOid, funder).AsBigInteger();
            BigInteger tokens = grant/capOfPP; // --PENDING-- rever unidades e cálculos
            BigInteger quota = tokens * sharesOfPP; // --PENDING-- rever unidades e cálculos
    
            Distribute(funder, quota, tokens);
            Transfer(null, funder, quota, tokens);
        }
    
        Process(id, "A new power plant is now operating.");
        return true;
    }
}


//---------------------------------------------------------------------------------------------
// METHODS FOR MEMBERS
// --> create
private static void Member( string address, string fullName, string utility, BigInteger quota, BigInteger tokens )
{
    Storage.Put( String.Concat( address, "FullName" ), fullName );
    Storage.Put( String.Concat( address, "Utility" ), utility );
    Storage.Put( String.Concat( address, "Quota" ), quota );
    Storage.Put( String.Concat( address, "Tokens" ), tokens );

    // Increases the total number of members.
    BigInteger temp = NumOfMemb() + 1;
    Storage.Put("NumOfMemb", temp);
    
    // Stores the address of each member.
    Storage.Put( String.Concat( "M", temp.ToString() ), address );
}

// --> read
private static byte[] GetMemb( string address, string opt )
{
    return Storage.Get( String.Concat( address, opt ) );
}

// --> update
// Detailed restrictions to update 'profile' or 'register' data are set
// on the function 'Change'. Here this feature is handled by polymorphism.
private static void UpMemb( string address, string opt, string val )
{
    // Don't invoke Put if value is unchanged.
    string orig = GetMemb(address, opt).AsString();
    if (orig == val) return;
     
    // Use Delete rather than Put if the new value is empty.
    if (val.Length == 0)
    {
       DelMemb(address, opt);
    }
    else
    {
       Storage.Put( String.Concat( address, opt ), val );
    }
}

private static void UpMemb( string address, string opt, BigInteger val )
{
    // Don't invoke Put if value is unchanged.
    BigInteger orig = GetMemb(address, opt).AsBigInteger();
    if (orig == val) return;
     
    // Use Delete rather than Put if the new value is zero.
    if (val == 0)
    {
       DelMemb(address, opt);
    }
    else
    {
       Storage.Put( String.Concat( address, opt ), val );
    }
}

// --> delete
private static void DelMemb( string address, string opt = "" )
{
    // If a member exits the group.
    if (opt == "")
    {
        Storage.Delete( String.Concat( address, "FullName" ) );
        Storage.Delete( String.Concat( address, "Utility" ) );
        Storage.Delete( String.Concat( address, "Quota" ) );
        Storage.Delete( String.Concat( address, "Tokens" ) );
        
        // Decreases the total number of members.
        BigInteger temp = NumOfMemb() - 1;
        Storage.Put("NumOfMemb", temp);
        
        // Wipe off the address of the member.
        Storage.Delete( String.Concat( "M", ? ), address ); // -- PENDING --
    }

    // To support an economic action for the update method.
    Storage.Delete( String.Concat( address, opt ) );
}

//---------------------------------------------------------------------------------------------
// METHODS FOR POWER PLANTS
// --> create
private static string PP( string capacity, BigInteger cost, string utility )
{
    string id = ID("P", capacity, cost, utility);
    if ( GetPP(id, "Capacity").Length != 0 )
    {
        Process(id, "This power plant already exists. Use the method UpPP to change its registering data.");
        return;
    }
    
    Storage.Put( String.Concat( id, "Capacity" ), capacity );
    Storage.Put( String.Concat( id, "Cost" ), cost );
    Storage.Put( String.Concat( id, "Utility" ), utility );
    // Storage.Put( String.Concat( id, "NumOfFundMemb" ), 0 ); // Expensive to create with null value. Just state it out!

    // Increases the total number of power plant units.
    BigInteger temp = NumOfPP() + 1;
    Storage.Put("NumOfPP", temp);
    
    // Stores the ID of each PP.
    Storage.Put( String.Concat( "P", temp.ToString() ), id );

    Process(id, "New PP created.")
    return id;
}

// --> read
private static byte[] GetPP( string id, string opt )
{
    return Storage.Get( String.Concat( id, opt ) );
}

// --> update
// The 'utility' is the only option that can be changed.
// To update the other options, delete the current PP and create a new one.
private static void UpPP( string id, string val )
{
    // Don't invoke Put if value is unchanged.
    string orig = GetPP(id, "Utility").AsString();
    if (orig == val) return;
    
    // Do nothing if the new value is empty.
    if (val.Length == 0) return;
    
    // else
    Storage.Put( String.Concat( id, "Utility" ), val );
    // And must 'update' each member 'utility' field as well.
    // 'Utility' should be a pointer and similar to 'Member' dataset.
}

// --> delete
private static void DelPP( string id )
{
    Storage.Delete( String.Concat( id, "Capacity" ) );
    Storage.Delete( String.Concat( id, "Cost" ) );
    Storage.Delete( String.Concat( id, "Utility" ) );
    if ( GetPP(id, "NumOfFundMemb") != 0 ) Storage.Delete( String.Concat( id, "NumOfFundMemb" ) );

    // Decreases the total number of power plant units.
    BigInteger temp = NumOfPP() - 1;
    Storage.Put("NumOfPP", temp);

    // Decreases the total power supply of power plants.
    BigInteger temp = TotalSupply() - GetPP(id, "Capacity").AsBigInteger();
    Storage.Put("TotalSupply", temp);
    
    // Wipe off the id of the PP.
    Storage.Delete( String.Concat( "P", ? ), id ); // -- PENDING --
}

//---------------------------------------------------------------------------------------------
// METHODS FOR REFERENDUMS
// --> create
private static string Ref( string proposal, string notes, int cost = 0 )
{
    string id = ID("R", proposal, notes, cost);
    if ( GetRef(id, "Proposal").Length != 0 )
    {
        Process(id, "This referendum already exists. Use the method UpRef to change its registering data, or just start a new referendum process.");
        return "-";
    }
    
    Storage.Put( String.Concat( id, "Proposal" ), proposal );
    Storage.Put( String.Concat( id, "Notes" ), notes );
    Storage.Put( String.Concat( id, "Cost" ), cost );
    // Storage.Put( String.Concat( id, "MoneyRaised" ), 0 ); // Expensive to create with null value. Just state it out!
    // Storage.Put( String.Concat( id, "NumOfVotes"), 0 );   // Expensive to create with null value. Just state it out!
    // Storage.Put( String.Concat( id, "CountTrue"), 0 );    // Expensive to create with null value. Just state it out!
    Storage.Put( String.Concat( id, "Outcome" ), Bool2Str(false) );
    Storage.Put( String.Concat( id, "startTime" ), StartTime() );
    Storage.Put( String.Concat( id, "endTime" ), StartTime() + timeframeRef );

    Process(id, "The referendum process has started.");
    return id;
}

// The function to vote on a referendum is declared above because it is public.

// --> read
private static byte[] GetRef( string id, string opt )       // retorna byte[] OU object? --PENDING--
{
    return Storage.Get( String.Concat( id, opt ) );
}

// --> update
// It is only possible to internally change the 'MoneyRaised', the 'NumOfVotes', the 'CountTrue' and the 'Outcome'.
private static void UpRef( string id, string opt, BigInteger val )
{
    if ((opt == "NumOfVotes") || (opt == "MoneyRaised") || (opt == "CountTrue"))
    {
        // Don't invoke Put if value is unchanged.
        BigInteger orig = GetRef(id, opt).AsBigInteger();
        if (orig == val) return;
         
        // Delete the storage if the new value is zero.
        if (val == 0) return Storage.Delete( String.Concat(id, opt) );
        
        // else
        Storage.Put( String.Concat( id, opt ), val );
    }
}

private static void UpRef( string id, bool val )
{
    // Don't invoke Put if value is unchanged.
    string orig = Str2Bool( GetRef(id, "Outcome") );
    if ( orig == Bool2Str(val) ) return;
        
    // else
    Storage.Put( String.Concat( id, "Outcome" ), Bool2Str(val) );
}

// --> delete
// A referendum process remains forever... and ever.


//---------------------------------------------------------------------------------------------
// METHODS TO EVALUATE A NEW POWER PLANT (aka an ICO of a NFT)
// --> create
private static void CrowdFunding( string ICOid )
{
    Storage.Put( String.Concat( ICOid, "startTime" ), StartTime() );
    Storage.Put( String.Concat( ICOid, "endTime" ), StartTime() + timeframeCrowd );
    // Storage.Put( String.Concat( ICOid, "TotalAmount" ), 0 );   // Expensive to create with null value. Just state it out!
    // Storage.Put( String.Concat( ICOid, "Contributions" ), 0 ); // Expensive to create with null value. Just state it out!
    Storage.Put( String.Concat( ICOid, "Success" ), Bool2Str(false) );
}

// The function to bid on a crowdfunding is declared above because it is public.

// --> read
private static BigInteger GetBid( string ICOid, string member )
{
    return Storage.Get( String.Concat( ICOid, member ) );
}

private static object GetCrowd( string ICOid, string opt )             // retorna byte[] OU object? --PENDING--
{
    return Storage.Get( String.Concat( ICOid, opt ) );
}

// --> update
private static bool UpBid( string ICOid, string member, BigInteger bid )
{
    // Don't invoke Put if value is unchanged.
    BigInteger orig = GetBid(ICOid, member).AsBigInteger();
    if (orig == bid) return;
     
    // Delete the storage if the new value is zero.
    if (bid == 0) return Refund(ICOid, member);                               Storage.Delete( String.Concat(id, opt) );
    
    // else
    Storage.Put( String.Concat( ICOid, member ), bid );
    return true;
}

// Only the 'Total Amount', 'Contributions' and 'Success' can be updated.
private static void UpCrowd( string ICOid, string opt, BigInteger val )
{
    if ( (opt == "TotalAmount") || (opt == "Contributions") )
    {
        // Don't invoke Put if value is unchanged.
        BigInteger orig = GetCrowd(ICOid, opt).AsBigInteger();
        if (orig == val) return;
         
        // Delete the storage if the new value is zero.
        if (val == 0) return DelCrowd(ICOid, opt);
        
        // else
        Storage.Put( String.Concat( ICOid, opt ), val );
    }
}

private static void UpCrowd( string ICOid, bool val )
{
    // Don't invoke Put if value is unchanged.
    string orig = Str2Bool( GetCrowd(ICOid, "Success") );
    if ( orig == Bool2Str(val) ) return;
        
    // else
    Storage.Put( String.Concat( ICOid, "Success" ), Bool2Str(val) );
}

// --> delete
private static void Refund( string ICOid, string member )
{
    // Deletes the member's offer.
    BigInteger grant = GetBid(ICOid, member);
    Storage.Delete( String.Concat( ICOid, member ) );
    
    // Decreases the total amount of funds
    BigInteger funds = GetCrowd(ICOid, "TotalAmount");
    UpCrowd(PPi, "TotalAmount", funds - grant);

    // Decreases the total number of contributions.
    BigInteger contributions = GetCrowd(ICOid, "Contributions");
    UpCrowd(ICOid, "Contributions", contributions--);
    
    // Sends the money back to the member.
    Trade(ICOid, member, 0, grant); // --PENDING-- aqui é SEB ou REAIS?
    Refund(member, grant);
}

// Only the 'Total Amount' and 'Contributions' can be "deleted"
// because the failure of a crowdfunding must be preserved.
// Actually it is only used to "store" null values cheaply.
private static void DelCrowd( string ICOid, string opt )
{
    if ( (opt == "TotalAmount") || (opt == "Contributions") )
    {
        Storage.Delete( String.Concat( ICOid, opt ) );
    }
}


//---------------------------------------------------------------------------------------------
https://github.com/neo-project/examples/blob/master/csharp/NEP5/NEP5.cs




// TO TEST
//---------------------------------------------------------------------------------------------

Neo.Header.GetTimestamp         // Get the timestamp of the block
Neo.Storage.GetContext          // [New] Get the current store context
Neo.Contract.GetStorageContext  // [New] Get the storage context of the contract

// get sender script hash
Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
TransactionOutput[] reference = tx.GetReferences();
reference[0].ScriptHash;

// to update ID and comparation of IDs operations
    string temp1 = String.Concat( "Hello world", "outra coisa" );
    string temp2 = String.Concat( "P", temp1);

    byte[] result2 = temp2.AsByteArray();

    Runtime.Notify( result2.AsString()[0] == 'P' ); // comparação entre string's, mas "P" não funciona...
    
    
    
    
    
    
    
    
    
// TO DO
//---------------------------------------------------------------------------------------------
// It must be an offline operation! From an offline monitoring, any Neo user could continue the process invoking the function again. However, it will only work if the user has a membership ID.

// CRIAR UMA OPERAÇÃO DA WALLET QUE POSSA FAZER ISSO! Exemplos de wallet?

// ---------------

// To unlock some operations to keep going.
// It automatically invokes this smart contract to continue a function from where it has been locked.
private static void Unlock(func equivalencia?) // como passar o comando para uma função específica?
{
    // Blockchain... Execute ( Owner(), function );
    Blockchain.GetAccount( Owner() ); // Get an account based on the scripthash of the contract
    Blockchain.GetAccount( Owner() ); // Get contract content based on contract hash
    Transaction.GetHash; //	Get Hash for the current transaction
    Transaction.GetAttributes; //	Query all properties of the current transaction
    Account.GetScriptHash; //	Get the script hash of the contract account
    Contract.GetScript; //	Get the scripthash of the contract
    
}
