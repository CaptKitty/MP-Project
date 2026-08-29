using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Linq;
using System.Text;

public class LoadProvinces : MonoBehaviour
{
    private const int MaximumProvincesPerRegion = 4;
    private const int InitialRegionTarget = 12;
    public List<Province> provincelist;
    
    public void LoadStuff()
    {



        LoadinProvinces();
        InitializeCulturalMixes();
        Owners.Instance.provincelist.Clear();
        Owners.Instance.provincelist = provincelist;
        AddStates();
        LoadProvincesinStates();
        BuildRegions();
    }

    public void LoadinCultures()
    {
        var txtarray = Resources.LoadAll("culturedatas", typeof(TextAsset));
        
        // using var sr = new StringReader(txt.text);
        int count = 0;  
        string Lines;
        int i = 0;
        foreach (TextAsset item in txtarray)
        {
            using var sr = new StringReader(item.text);
            string line = "potato";
            Culture culture = new Culture();
            while (line != null)
            {
                // Debug.Log(line);
                if(line.Contains("Name"))
                {
                    line = sr.ReadLine();
                    culture.name = line.Trim( );
                    // Debug.Log(provincename);
                }
                if(line.Contains("Color"))
                {
                    line = sr.ReadLine();
                    byte red = byte.Parse(line);
                    byte green = byte.Parse(sr.ReadLine());
                    byte blue = byte.Parse(sr.ReadLine());
                    culture.ownerIdentity = new Color32(red,green,blue,0);
                    // Debug.Log(color);
                }
                line = sr.ReadLine();
            }
            Owners.Instance.culturelist.Add(culture);
        }
    }

    void LoadinProvinces()
    {
        TextAsset txt = (TextAsset)Resources.Load("provincedata/Amsterdam", typeof(TextAsset));
        var txtarray = Resources.LoadAll("provincedatas", typeof(TextAsset));

        // var txtarray = new List<TextAsset>();
        // if(1==1)
        // {
        //     txtarray.Clear();
        //     var info = new DirectoryInfo(Application.streamingAssetsPath+ "/provincedatas");
        //     var fileInfo = info.GetFiles();
        //     foreach(FileInfo item in fileInfo)
        //     {
        //         if (item.Exists)
        //         {
        //             // Read the file contents
        //             string fileContent = File.ReadAllText(item.FullName);

        //             // Create a new TextAsset from the file contents
        //             TextAsset textAsset = new TextAsset(fileContent);
        //             txtarray.Add(textAsset);
        //         }   
        //     }
        // }
        
        
        // using var sr = new StringReader(txt.text);
        int count = 0;  
        string Lines;
        int i = 0;
        foreach (TextAsset item in txtarray)
        {
            // Debug.Log(item);
            using var sr = new StringReader(item.text);
            string line = "potato";

            string provincename = "Error";
            string regionname = string.Empty;
            Color32 color = new Color32(0,0,0,0);
            Vector2 location = new Vector2(0,0);
            int population = 1;
            Nation nation = new Nation();
            Province newprovince = new Province();
            newprovince.cultures = new List<Culture>();
            Culture culture = new Culture();
            while (line != null)
            {
                // Debug.Log(line);
                if(line.Contains("Name"))
                {
                    line = sr.ReadLine();
                    provincename = line.Trim( );
                    // Debug.Log(provincename);
                }
                if(line.Contains("Color"))
                {
                    line = sr.ReadLine();
                    byte red = byte.Parse(line);
                    byte green = byte.Parse(sr.ReadLine());
                    byte blue = byte.Parse(sr.ReadLine());
                    color = new Color32(red,green,blue,0);
                    // Debug.Log(color);
                }
                if(line.Contains("Location"))
                {
                    line = sr.ReadLine();
                    int x = int.Parse(line);
                    int y = int.Parse(sr.ReadLine());
                    location = new Vector2(x,y);
                    // Debug.Log(location);
                }
                // if(line.Contains("Population"))
                // {
                //     line = sr.ReadLine();
                //     population = int.Parse(line);
                //     culture = new Culture();
                //     culture.population = population;
                //     line = sr.ReadLine();
                //     if(line == "}")
                //     {
                //         //line = "Dutch";
                //         line = "None";
                //     }
                //     culture.name = line;
                //     culture.ownerIdentity = Owners.Instance.CallCultureByName(line).ownerIdentity;
                //     culture.name = Owners.Instance.CallCultureByName(line).name;
                //     newprovince.cultures.Add(culture);
                    
                //     // Debug.Log(population);
                // }
                if(line.Contains("Owner"))
                {
                    line = sr.ReadLine();
                    line = sr.ReadLine();
                    newprovince.nation = GetNation(line.Trim( ));
                    // Debug.Log(nation);
                }
                if(line.Contains("Region"))
                {
                    line = sr.ReadLine();
                    regionname = line == null ? string.Empty : line.Trim();
                }
                line = sr.ReadLine();
            }
            if(culture.name == "None")
            {
                if(newprovince.nation.name == "France")
                {
                    culture.name = "French";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
                if(newprovince.nation.name == "Spain")
                {
                    culture.name = "Spanish";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
                if(newprovince.nation.name == "Portugal")
                {
                    culture.name = "Portuguese";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
                if(newprovince.nation.name == "Netherlands")
                {
                    culture.name = "Dutch";
                    culture.ownerIdentity = Owners.Instance.CallCultureByName(culture.name).ownerIdentity;
                }
            }
            
            newprovince.name = provincename;
            newprovince.regionConfiguredFromData = !string.IsNullOrWhiteSpace(regionname);
            newprovince.region = string.IsNullOrWhiteSpace(regionname)
                ? DeriveRegionName(provincename)
                : regionname;
            newprovince.identity = color;
            newprovince.position = location;
            ApplyOptionalProvinceData(item.text, newprovince, provincename);
            newprovince.EnsureCulture();
            newprovince.UpdatePopulation();
            // newprovince.population = population;
            
            // newprovince.nation = nation;

            provincelist.Add(newprovince);
        }        
    }

    void BuildRegions()
    {
        Owners.Instance.regionlist = new List<CampaignRegion>();
        Owners.Instance.regiondict = new Dictionary<string, CampaignRegion>(StringComparer.OrdinalIgnoreCase);

        List<Province> remaining = Owners.Instance.provincelist
            .Where(province => province != null)
            .OrderBy(province => province.name, StringComparer.OrdinalIgnoreCase).ToList();

        // Region blocks in province data are authoritative. Register those
        // groups first and leave only legacy/unconfigured provinces for the
        // automatic geographic region builder.
        foreach (IGrouping<string, Province> configuredGroup in remaining
            .Where(province => province.regionConfiguredFromData && !string.IsNullOrWhiteSpace(province.region))
            .GroupBy(province => province.region.Trim(), StringComparer.OrdinalIgnoreCase).ToList())
        {
            List<Province> members = configuredGroup.OrderBy(province => province.name,
                StringComparer.OrdinalIgnoreCase).ToList();
            CampaignRegion configured = new CampaignRegion
            {
                name = configuredGroup.Key,
                configuredFromProvinceData = true,
                identity = RegionColor(Owners.Instance.regionlist.Count),
                loyalty = 100f,
                provincelist = members
            };
            foreach (Nation owner in members.Where(province => province.nation != null)
                .Select(province => province.nation).Distinct()) configured.SetLoyalty(owner, 100f);
            Owners.Instance.regiondict[configured.name] = configured;
            Owners.Instance.regionlist.Add(configured);
            foreach (Province member in members) remaining.Remove(member);
        }
        foreach (Province province in remaining)
            if (string.IsNullOrWhiteSpace(province.region)) province.region = DeriveRegionName(province.name);

        int regionsRemaining = Mathf.CeilToInt(remaining.Count / (float)InitialRegionTarget);
        Dictionary<string, int> nameUses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (remaining.Count > 0)
        {
            int targetSize = Mathf.Clamp(Mathf.CeilToInt(remaining.Count / (float)Mathf.Max(1, regionsRemaining)), 3,
                InitialRegionTarget);
            targetSize = Mathf.Min(targetSize, remaining.Count);
            Province seed = ChooseRegionSeed(remaining);
            List<Province> members = new List<Province> { seed };
            remaining.Remove(seed);

            while (members.Count < targetSize && remaining.Count > 0)
            {
                Province next = remaining
                    .Where(candidate => IsAdjacentToRegion(candidate, members))
                    .OrderBy(candidate => RegionShapeScore(candidate, members))
                    .ThenByDescending(candidate => RemainingDegree(candidate, remaining))
                    .ThenBy(candidate => candidate.name, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (next == null) break;
                members.Add(next);
                remaining.Remove(next);
            }

            string baseName = string.IsNullOrWhiteSpace(seed.region) ? DeriveRegionName(seed.name) : seed.region;
            nameUses.TryGetValue(baseName, out int useCount);
            nameUses[baseName] = ++useCount;
            string regionName = useCount == 1 && !Owners.Instance.regiondict.ContainsKey(baseName)
                ? baseName : baseName + " (" + useCount + ")";
            while (Owners.Instance.regiondict.ContainsKey(regionName))
                regionName = baseName + " (" + (++useCount) + ")";
            nameUses[baseName] = useCount;

            CampaignRegion region = new CampaignRegion
            {
                name = regionName,
                identity = RegionColor(Owners.Instance.regionlist.Count),
                loyalty = 100f,
                provincelist = members
            };
            foreach (Nation owner in members.Where(province => province != null && province.nation != null)
                .Select(province => province.nation).Distinct()) region.SetLoyalty(owner, 100f);
            foreach (Province province in members) province.region = regionName;
            Owners.Instance.regiondict.Add(regionName, region);
            Owners.Instance.regionlist.Add(region);
            regionsRemaining--;
        }
        SplitOversizedRegions();
        AttachLooseRegions();
        BalanceTwoProvinceRegions();
        RenameRegionsFromCentralProvinces();
    }

    void RenameRegionsFromCentralProvinces()
    {
        Dictionary<CampaignRegion, string> baseNames = new Dictionary<CampaignRegion, string>();
        Dictionary<CampaignRegion, Vector2> centers = new Dictionary<CampaignRegion, Vector2>();
        foreach (CampaignRegion region in Owners.Instance.regionlist)
        {
            if (region == null || region.provincelist == null || region.provincelist.Count == 0) continue;
            if (region.configuredFromProvinceData) continue;
            Vector2 center = RegionCenter(region);
            Province centralProvince = region.provincelist
                .Where(province => province != null)
                .OrderBy(province => Vector2.SqrMagnitude(province.position - center))
                .ThenBy(province => province.name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            centers[region] = center;
            baseNames[region] = centralProvince != null ? DeriveRegionName(centralProvince.name) : "Unassigned";
        }

        Dictionary<CampaignRegion, string> finalNames = new Dictionary<CampaignRegion, string>();
        foreach (IGrouping<string, CampaignRegion> group in baseNames.Keys
            .GroupBy(region => baseNames[region], StringComparer.OrdinalIgnoreCase))
        {
            List<CampaignRegion> duplicates = group.ToList();
            if (duplicates.Count == 1)
            {
                finalNames[duplicates[0]] = group.Key;
                continue;
            }

            Vector2 groupCenter = Vector2.zero;
            foreach (CampaignRegion region in duplicates) groupCenter += centers[region];
            groupCenter /= duplicates.Count;

            if (duplicates.Count == 2)
            {
                CampaignRegion first = duplicates[0];
                CampaignRegion second = duplicates[1];
                Vector2 separation = centers[first] - centers[second];
                bool vertical = Mathf.Abs(separation.y) >= Mathf.Abs(separation.x);
                if (vertical)
                {
                    CampaignRegion north = centers[first].y >= centers[second].y ? first : second;
                    CampaignRegion south = north == first ? second : first;
                    finalNames[north] = "North " + group.Key;
                    finalNames[south] = "South " + group.Key;
                }
                else
                {
                    CampaignRegion east = centers[first].x >= centers[second].x ? first : second;
                    CampaignRegion west = east == first ? second : first;
                    finalNames[east] = "East " + group.Key;
                    finalNames[west] = "West " + group.Key;
                }
                continue;
            }

            foreach (CampaignRegion region in duplicates)
            {
                Vector2 offset = centers[region] - groupCenter;
                string direction = Mathf.Abs(offset.y) >= Mathf.Abs(offset.x)
                    ? (offset.y >= 0f ? "North " : "South ")
                    : (offset.x >= 0f ? "East " : "West ");
                finalNames[region] = direction + group.Key;
            }
        }

        Owners.Instance.regiondict = new Dictionary<string, CampaignRegion>(StringComparer.OrdinalIgnoreCase);
        foreach (CampaignRegion configured in Owners.Instance.regionlist
            .Where(region => region != null && region.configuredFromProvinceData))
        {
            string configuredName = configured.name;
            int suffix = 2;
            while (Owners.Instance.regiondict.ContainsKey(configuredName))
                configuredName = configured.name + " (" + suffix++ + ")";
            configured.name = configuredName;
            foreach (Province province in configured.provincelist)
                if (province != null) province.region = configuredName;
            Owners.Instance.regiondict.Add(configuredName, configured);
        }
        Dictionary<string, int> collisions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (CampaignRegion region in Owners.Instance.regionlist)
        {
            if (region == null || !finalNames.TryGetValue(region, out string requestedName)) continue;
            collisions.TryGetValue(requestedName, out int count);
            collisions[requestedName] = ++count;
            string uniqueName = count == 1 ? requestedName : requestedName + " (" + count + ")";
            while (Owners.Instance.regiondict.ContainsKey(uniqueName))
                uniqueName = requestedName + " (" + (++count) + ")";
            collisions[requestedName] = count;

            region.name = uniqueName;
            foreach (Province province in region.provincelist)
                if (province != null) province.region = uniqueName;
            Owners.Instance.regiondict.Add(uniqueName, region);
        }
    }

    static Vector2 RegionCenter(CampaignRegion region)
    {
        Vector2 center = Vector2.zero;
        int count = 0;
        foreach (Province province in region.provincelist)
        {
            if (province == null) continue;
            center += province.position;
            count++;
        }
        return count > 0 ? center / count : Vector2.zero;
    }

    void SplitOversizedRegions()
    {
        foreach (CampaignRegion oversized in Owners.Instance.regionlist
            .Where(region => !region.configuredFromProvinceData &&
                region.provincelist.Count > MaximumProvincesPerRegion).ToList())
        {
            int splitNumber = 2;
            while (oversized.provincelist.Count > MaximumProvincesPerRegion)
            {
                int groupsNeeded = Mathf.CeilToInt(oversized.provincelist.Count /
                    (float)MaximumProvincesPerRegion);
                int desiredSize = Mathf.CeilToInt(oversized.provincelist.Count / (float)groupsNeeded);
                List<Province> splitMembers = FindBestConnectedSubset(oversized.provincelist, desiredSize);
                if (splitMembers == null)
                {
                    Province detachable = oversized.provincelist.FirstOrDefault(province =>
                        ProvincesAreConnected(oversized.provincelist.Where(other => other != province).ToList()));
                    if (detachable == null) break;
                    splitMembers = new List<Province> { detachable };
                }
                foreach (Province province in splitMembers) oversized.provincelist.Remove(province);
                string splitName = UniqueRegionName(oversized.name + " Part " + splitNumber++);
                CampaignRegion split = new CampaignRegion
                {
                    name = splitName,
                    identity = RegionColor(Owners.Instance.regionlist.Count),
                    loyalty = 100f,
                    provincelist = splitMembers
                };
                foreach (Nation owner in splitMembers.Where(province => province != null && province.nation != null)
                    .Select(province => province.nation).Distinct()) split.SetLoyalty(owner, 100f);
                foreach (Province province in splitMembers) province.region = splitName;
                Owners.Instance.regiondict.Add(splitName, split);
                Owners.Instance.regionlist.Add(split);
            }
            foreach (Province province in oversized.provincelist) province.region = oversized.name;
        }
    }

    void InitializeCulturalMixes()
    {
        List<Province> provinces = provincelist.Where(province => province != null).ToList();
        Dictionary<Province, Culture> originalCultures = new Dictionary<Province, Culture>();
        foreach (Province province in provinces)
        {
            province.EnsureCulture();
            originalCultures[province] = province.PrimaryCulture;
        }

        foreach (Province province in provinces)
        {
            Culture primary = originalCultures[province];
            int totalPopulation = Mathf.Max(1, province.population);
            List<Culture> minorities = provinces.Where(other => other != province &&
                    originalCultures[other] != null && primary != null &&
                    !string.Equals(originalCultures[other].name, primary.name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(other => Vector2.Distance(province.position, other.position))
                .Select(other => originalCultures[other])
                .GroupBy(culture => culture.name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).Take(2).ToList();

            int firstMinority = minorities.Count > 0 ? Mathf.RoundToInt(totalPopulation * .15f) : 0;
            int secondMinority = minorities.Count > 1 ? Mathf.RoundToInt(totalPopulation * .05f) : 0;
            if (minorities.Count == 1) firstMinority = Mathf.RoundToInt(totalPopulation * .20f);
            province.cultures = new List<Culture>
            {
                new Culture
                {
                    name = primary != null ? primary.name : "Unassigned",
                    ownerIdentity = primary != null ? primary.ownerIdentity : province.identity,
                    population = totalPopulation - firstMinority - secondMinority
                }
            };
            if (minorities.Count > 0) province.cultures.Add(new Culture
            {
                name = minorities[0].name, ownerIdentity = minorities[0].ownerIdentity, population = firstMinority
            });
            if (minorities.Count > 1) province.cultures.Add(new Culture
            {
                name = minorities[1].name, ownerIdentity = minorities[1].ownerIdentity, population = secondMinority
            });
            province.UpdatePopulation();
        }
    }

    static List<Province> FindBestConnectedSubset(List<Province> members, int desiredSize)
    {
        if (desiredSize <= 0 || desiredSize >= members.Count || members.Count > 30) return null;
        List<Province> best = null;
        float bestScore = float.MaxValue;
        int limit = 1 << members.Count;
        for (int mask = 1; mask < limit; mask++)
        {
            int bits = 0;
            for (int value = mask; value != 0; value &= value - 1) bits++;
            if (bits != desiredSize) continue;
            List<Province> subset = new List<Province>();
            List<Province> remainder = new List<Province>();
            for (int index = 0; index < members.Count; index++)
                if ((mask & 1 << index) != 0) subset.Add(members[index]); else remainder.Add(members[index]);
            if (!ProvincesAreConnected(subset) || !ProvincesAreConnected(remainder)) continue;
            float score = ProvinceGroupShapeScore(subset) + ProvinceGroupShapeScore(remainder);
            if (score < bestScore) { bestScore = score; best = subset; }
        }
        return best;
    }

    string UniqueRegionName(string requestedName)
    {
        if (!Owners.Instance.regiondict.ContainsKey(requestedName)) return requestedName;
        int suffix = 2;
        string candidate;
        do candidate = requestedName + " (" + suffix++ + ")";
        while (Owners.Instance.regiondict.ContainsKey(candidate));
        return candidate;
    }

    static bool ProvincesAreConnected(List<Province> provinces)
    {
        if (provinces.Count <= 1) return true;
        HashSet<Province> visited = new HashSet<Province> { provinces[0] };
        Queue<Province> frontier = new Queue<Province>();
        frontier.Enqueue(provinces[0]);
        while (frontier.Count > 0)
        {
            Province current = frontier.Dequeue();
            foreach (Province province in provinces)
                if (!visited.Contains(province) && AreAdjacent(current, province))
                {
                    visited.Add(province);
                    frontier.Enqueue(province);
                }
        }
        return visited.Count == provinces.Count;
    }

    static float ProvinceGroupShapeScore(List<Province> provinces)
    {
        Vector2 centroid = Vector2.zero;
        foreach (Province province in provinces) centroid += province.position;
        centroid /= provinces.Count;
        float score = 0f;
        foreach (Province province in provinces) score += Vector2.SqrMagnitude(province.position - centroid);
        return score / provinces.Count;
    }

    void BalanceTwoProvinceRegions()
    {
        foreach (CampaignRegion receiver in Owners.Instance.regionlist
            .Where(region => !region.configuredFromProvinceData && region.provincelist.Count == 2)
            .OrderBy(region => region.name, StringComparer.OrdinalIgnoreCase).ToList())
        {
            Province bestProvince = null;
            CampaignRegion bestDonor = null;
            float bestScore = float.MaxValue;
            foreach (CampaignRegion donor in Owners.Instance.regionlist
                .Where(region => !region.configuredFromProvinceData &&
                    region.provincelist.Count == MaximumProvincesPerRegion))
            {
                foreach (Province candidate in donor.provincelist)
                {
                    if (!receiver.provincelist.Exists(member => AreAdjacent(candidate, member)) ||
                        !RegionRemainsConnectedWithout(donor, candidate)) continue;
                    float score = RegionShapeScore(candidate, receiver.provincelist);
                    if (score < bestScore || Mathf.Approximately(score, bestScore) &&
                        string.CompareOrdinal(candidate.name, bestProvince != null ? bestProvince.name : string.Empty) < 0)
                    {
                        bestScore = score;
                        bestProvince = candidate;
                        bestDonor = donor;
                    }
                }
            }
            if (bestProvince == null || bestDonor == null) continue;
            bestDonor.provincelist.Remove(bestProvince);
            receiver.provincelist.Add(bestProvince);
            bestProvince.region = receiver.name;
        }
    }

    static bool RegionRemainsConnectedWithout(CampaignRegion region, Province removed)
    {
        List<Province> remaining = region.provincelist.Where(province => province != removed).ToList();
        if (remaining.Count <= 1) return true;
        HashSet<Province> visited = new HashSet<Province>();
        Queue<Province> frontier = new Queue<Province>();
        frontier.Enqueue(remaining[0]);
        visited.Add(remaining[0]);
        while (frontier.Count > 0)
        {
            Province current = frontier.Dequeue();
            foreach (Province province in remaining)
                if (!visited.Contains(province) && AreAdjacent(current, province))
                {
                    visited.Add(province);
                    frontier.Enqueue(province);
                }
        }
        return visited.Count == remaining.Count;
    }

    void AttachLooseRegions()
    {
        bool merged;
        do
        {
            merged = false;
            List<CampaignRegion> looseRegions = Owners.Instance.regionlist
                .Where(region => !region.configuredFromProvinceData && region.provincelist.Count < 3)
                .OrderBy(region => region.provincelist.Count).ThenBy(region => region.name,
                    StringComparer.OrdinalIgnoreCase).ToList();
            foreach (CampaignRegion loose in looseRegions)
            {
                CampaignRegion target = Owners.Instance.regionlist
                    .Where(region => region != loose && !region.configuredFromProvinceData &&
                        region.provincelist.Count + loose.provincelist.Count <= MaximumProvincesPerRegion &&
                        RegionsAreAdjacent(loose, region))
                    .OrderBy(region => CombinedRegionShapeScore(loose, region))
                    .ThenBy(region => region.name, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (target == null) continue;

                foreach (Province province in loose.provincelist)
                {
                    province.region = target.name;
                    target.provincelist.Add(province);
                }
                Owners.Instance.regiondict.Remove(loose.name);
                Owners.Instance.regionlist.Remove(loose);
                merged = true;
                break;
            }
        } while (merged);
    }

    static bool RegionsAreAdjacent(CampaignRegion first, CampaignRegion second)
    {
        return first.provincelist.Exists(a => second.provincelist.Exists(b => AreAdjacent(a, b)));
    }

    static float CombinedRegionShapeScore(CampaignRegion first, CampaignRegion second)
    {
        List<Province> members = new List<Province>(first.provincelist);
        members.AddRange(second.provincelist);
        Vector2 centroid = Vector2.zero;
        foreach (Province province in members) centroid += province.position;
        centroid /= members.Count;
        float score = 0f;
        foreach (Province province in members) score += Vector2.SqrMagnitude(province.position - centroid);
        return score / members.Count;
    }

    static Province ChooseRegionSeed(List<Province> provinces)
    {
        return provinces.OrderByDescending(province => provinces.Count(other => other != province && AreAdjacent(province, other)))
            .ThenBy(province => province.name, StringComparer.OrdinalIgnoreCase).First();
    }

    static bool IsAdjacentToRegion(Province province, List<Province> region)
    {
        return region.Exists(member => AreAdjacent(province, member));
    }

    static bool AreAdjacent(Province first, Province second)
    {
        return first != null && second != null && Vector2.Distance(first.position, second.position) < 50f;
    }

    static int RemainingDegree(Province province, List<Province> remaining)
    {
        return remaining.Count(other => other != province && AreAdjacent(province, other));
    }

    static float RegionShapeScore(Province candidate, List<Province> members)
    {
        Vector2 centroid = candidate.position;
        foreach (Province member in members) centroid += member.position;
        centroid /= members.Count + 1;

        float spread = Vector2.SqrMagnitude(candidate.position - centroid);
        float farthest = spread;
        foreach (Province member in members)
        {
            float distance = Vector2.SqrMagnitude(member.position - centroid);
            spread += distance;
            farthest = Mathf.Max(farthest, distance);
        }

        // Low variance and a short outer radius favor rounded clusters over chains.
        return spread / (members.Count + 1) + farthest * 2f;
    }

    static Color32 RegionColor(int regionIndex)
    {
        // Golden-ratio hue spacing keeps neighboring generated regions visually distinct.
        float hue = Mathf.Repeat(regionIndex * 0.61803398875f, 1f);
        float saturation = 0.62f + 0.12f * (regionIndex % 2);
        float value = 0.78f + 0.12f * ((regionIndex / 2) % 2);
        return (Color32)Color.HSVToRGB(hue, saturation, value);
    }

    static string DeriveRegionName(string provinceName)
    {
        if (string.IsNullOrWhiteSpace(provinceName)) return "Unassigned";
        int separator = provinceName.LastIndexOf('_');
        if (separator > 0 && separator < provinceName.Length - 1 &&
            int.TryParse(provinceName.Substring(separator + 1), out _))
            return provinceName.Substring(0, separator).Trim();
        return provinceName.Trim();
    }
    void LoadProvincesinStates()
    {
  
        foreach (Province province in Owners.Instance.provincelist)
        {
            foreach (State state in Owners.Instance.statelist)
            {
                if(state.nation.name == province.nation.name)
                {
                    if(state.stateIdentity == new Color(0,0,0,0))
                    {
                        state.stateIdentity = province.identity;
                    }
                    state.provincelist.Add(province);
                    province.state = state.name;
                }
            }
        }
    }
    void AddStates()
    {
        foreach (Nation nation in Owners.Instance.nationlist)
        {
            State state = new State();
            state.name = nation.name;
            state.nation = nation;
            state.taxpercentage = 10;
            state.levypercentage = 10;
            state.stateIdentity = new Color32(0,0,0,0);
            state.provincelist = new List<Province>();
            Owners.Instance.statelist.Add(state);
        }
    }

    Nation GetNation(string name)
    {
        foreach (Nation nation in Owners.Instance.nationlist)
        {
            // Debug.Log(name + " + " + nation.name);
            if(name == nation.name)
            {
                // Debug.Log(nation.ownerIdentity);
                return nation;
            }
        }
        return new Nation();
    }

    static void ApplyOptionalProvinceData(string text, Province province, string provinceName)
    {
        List<string> terrain = ReadBlock(text, "Terrain");
        if (terrain.Count > 0 && Enum.TryParse(terrain[0], true, out CampaignTerrainProfile terrainProfile))
            province.terrainProfile = terrainProfile;

        List<string> development = ReadBlock(text, "BaseDevelopment");
        if (development.Count > 0 && int.TryParse(development[0], out int baseDevelopment))
            province.baseMaximumDevelopment = Mathf.Max(0, baseDevelopment);

        List<string> urbanization = ReadBlock(text, "Urbanization");
        int startingUrbanization = 0;
        bool hasExplicitUrbanization = urbanization.Count > 0 && int.TryParse(urbanization[0], out startingUrbanization);
        if (hasExplicitUrbanization)
            province.urbanization = Mathf.Clamp(startingUrbanization, -100, province.MaximumDevelopment);

        List<string> holdingLines = ReadBlock(text, "Holdings");
        if (holdingLines.Count > 0)
        {
            province.holdings = new List<ProvinceHolding>();
            int slot = 0;
            foreach (string holdingLine in holdingLines)
            {
                string[] fields = SplitFields(holdingLine);
                if (fields.Length == 0 || string.IsNullOrWhiteSpace(fields[0])) continue;
                HoldingDefinition definition = HoldingDefinition.Find(fields[0]);
                if (definition == null) { Debug.LogWarning("Unknown holding '" + fields[0] + "' in " + provinceName); continue; }
                int count = fields.Length > 1 && int.TryParse(fields[1], out int parsedCount) ? Mathf.Max(0, parsedCount) : 1;
                string culture = fields.Length > 2 && !string.IsNullOrWhiteSpace(fields[2]) ? fields[2] : "Unassigned";
                string className = fields.Length > 3 && fields[3].Equals("Peasants", StringComparison.OrdinalIgnoreCase)
                    ? nameof(SocioEconomicClass.Freemen) : fields.Length > 3 ? fields[3] : string.Empty;
                SocioEconomicClass socialClass = fields.Length > 3 && Enum.TryParse(className, true, out SocioEconomicClass parsedClass)
                    ? parsedClass : definition.defaultClass;
                int level = fields.Length > 4 && int.TryParse(fields[4], out int parsedLevel) ? Mathf.Max(1, parsedLevel) : 1;
                bool levyEnabled = fields.Length <= 5 || !bool.TryParse(fields[5], out bool parsedLevy) || parsedLevy;
                string allegiance = fields.Length > 6 ? fields[6].Trim() : string.Empty;
                for (int index = 0; index < count; index++)
                    province.holdings.Add(new ProvinceHolding {
                        instanceId = provinceName + "-holding-" + slot, definition = definition,
                        id = definition.StableId, level = Mathf.Min(level, definition.maximumLevel), slotIndex = slot++,
                        cultureName = culture, socioEconomicClass = SocioEconomicClassRules.Normalize(socialClass), allegiance = allegiance,
                        levyEnabled = levyEnabled });
            }
        }

        List<string> buildingLines = ReadBlock(text, "Buildings");
        if (buildingLines.Count > 0)
        {
            province.buildings = new List<ProvinceBuilding>();
            foreach (string buildingLine in buildingLines)
            {
                string[] fields = SplitFields(buildingLine);
                if (fields.Length == 0 || string.IsNullOrWhiteSpace(fields[0])) continue;
                BuildingDefinition definition = BuildingDefinition.Find(fields[0]);
                if (definition == null) { Debug.LogWarning("Unknown building '" + fields[0] + "' in " + provinceName); continue; }
                int level = fields.Length > 1 && int.TryParse(fields[1], out int parsedLevel) ? Mathf.Max(1, parsedLevel) : 1;
                int slot = fields.Length > 2 && int.TryParse(fields[2], out int parsedSlot) ? Mathf.Max(0, parsedSlot) : province.buildings.Count;
                province.buildings.Add(new ProvinceBuilding { definition = definition, id = definition.StableId,
                    level = Mathf.Min(level, definition.maximumLevel), maxLevel = definition.maximumLevel, slotIndex = slot });
            }
        }

        List<string> modifierLines = ReadBlock(text, "Modifiers");
        if (modifierLines.Count > 0)
        {
            province.uniqueModifiers = new List<ProvinceNamedModifier>();
            foreach (string modifierLine in modifierLines)
            {
                string[] fields = SplitFields(modifierLine);
                if (fields.Length == 0 || string.IsNullOrWhiteSpace(fields[0])) continue;
                int maxDevelopment = fields.Length > 1 && int.TryParse(fields[1], out int parsedModifier) ? parsedModifier : 0;
                province.uniqueModifiers.Add(new ProvinceNamedModifier { name = fields[0],
                    localModifiers = new ProvinceLocalModifiers { maxDevelopment = maxDevelopment } });
            }
        }

        if (!hasExplicitUrbanization)
            province.urbanization = CultureStartingUrbanization(province);
        province.urbanization = Mathf.Clamp(province.urbanization, -100, province.MaximumDevelopment);
    }

    static int CultureStartingUrbanization(Province province)
    {
        string cultureName = null;
        if (province != null && province.holdings != null && province.holdings.Count > 0)
            cultureName = province.holdings.Where(holding => holding != null && !string.IsNullOrWhiteSpace(holding.cultureName))
                .GroupBy(holding => holding.cultureName.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Key).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(cultureName) && province != null && province.nation != null && province.nation.culture != null)
            cultureName = province.nation.culture.DisplayName;
        NationCultureData definition = Resources.LoadAll<NationCultureData>("Prefabs/NationData/Culture")
            .FirstOrDefault(candidate => candidate != null && candidate.Matches(cultureName));
        return definition != null ? Mathf.Clamp(definition.startingUrbanization, -100, 100) : 0;
    }

    static List<string> ReadBlock(string text, string blockName)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrEmpty(text)) return result;
        string[] lines = text.Replace("\r", string.Empty).Split('\n');
        bool reading = false;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (!reading)
            {
                if (line.StartsWith(blockName, StringComparison.OrdinalIgnoreCase) && line.Contains("=")) reading = true;
                continue;
            }
            if (line == "{") continue;
            if (line == "}") break;
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#")) result.Add(line);
        }
        return result;
    }

    static string[] SplitFields(string line) => line.Split(new[] { '|' }, StringSplitOptions.None)
        .Select(field => field.Trim()).ToArray();

    void LoadBasePopulation()
    {
        
    }
}
