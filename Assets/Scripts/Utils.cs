using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Tilemaps;

public class Utils {

    public static int FirstPlaceStars {
        get {
            return GameManager.Instance.allPlayers.Max(pc => pc.stars);
        }
    }


    public static Vector3Int WorldToTilemapPosition(Vector3 worldVec, GameManager manager = null, bool wrap = true) {
        if (!manager)
            manager = GameManager.Instance;
        Vector3Int tileLocation = manager.tilemap.WorldToCell(worldVec);
        if (wrap)
            WrapTileLocation(ref tileLocation, manager);

        return tileLocation;
    }

    public static bool WrapWorldLocation(ref Vector3 location, GameManager manager = null) {
        if (!manager)
            manager = GameManager.Instance;
        if (!manager.loopingLevel)
            return false;

        if (location.x < manager.GetLevelMinX()) {
            location.x += manager.levelWidthTile / 2;
            return true;
        } else if (location.x >= manager.GetLevelMaxX()) {
            location.x -= manager.levelWidthTile / 2;
            return true;
        }
        return false;
    }

    public static void WrapTileLocation(ref Vector3Int tileLocation, GameManager manager = null) {
        if (!manager)
            manager = GameManager.Instance;
        if (tileLocation.x < manager.levelMinTileX)
            tileLocation.x += manager.levelWidthTile;
        if (tileLocation.x >= manager.levelMinTileX + manager.levelWidthTile)
            tileLocation.x -= manager.levelWidthTile;
    }

    public static Vector3Int WorldToTilemapPosition(float worldX, float worldY) {
        return WorldToTilemapPosition(new Vector3(worldX, worldY));
    }

    public static Vector3 TilemapToWorldPosition(Vector3Int tileVec, GameManager manager = null) {
        if (!manager)
            manager = GameManager.Instance;
        return manager.tilemap.CellToWorld(tileVec);
    }

    public static Vector3 TilemapToWorldPosition(int tileX, int tileY) {
        return TilemapToWorldPosition(new Vector3Int(tileX, tileY));
    }

    public static int GetCharacterIndex(Player player = null) {
        if (player == null)
            player = PhotonNetwork.LocalPlayer;

        GetCustomProperty(Enums.NetPlayerProperties.Character, out int index, player.CustomProperties);
        return index;
    }
    public static PlayerData GetCharacterData(Player player = null) {
        return GlobalController.Instance.characters[GetCharacterIndex(player)];
    }

    public static TileBase GetTileAtTileLocation(Vector3Int tileLocation) {
        WrapTileLocation(ref tileLocation);
        return GameManager.Instance.tilemap.GetTile(tileLocation);
    }
    public static TileBase GetTileAtWorldLocation(Vector3 worldLocation) {
        return GetTileAtTileLocation(WorldToTilemapPosition(worldLocation));
    }

    public static bool IsTileSolidAtTileLocation(Vector3Int tileLocation) {
        WrapTileLocation(ref tileLocation);
        return GetColliderType(tileLocation) == Tile.ColliderType.Grid;
    }

    private static Tile.ColliderType GetColliderType(Vector3Int tileLocation) {

        if (GameManager.Instance.tilemap.GetTile<Tile>(tileLocation) is Tile tile)
            return tile.colliderType;

        if (GameManager.Instance.tilemap.GetTile<AnimatedTile>(tileLocation) is AnimatedTile animated)
            return animated.m_TileColliderType;

        if (GameManager.Instance.tilemap.GetTile<RuleTile>(tileLocation) is RuleTile rule)
            return rule.m_DefaultColliderType;

        return Tile.ColliderType.None;
    }

    public static bool IsTileSolidAtWorldLocation(Vector3 worldLocation) {
        Collider2D collision = Physics2D.OverlapPoint(worldLocation, LayerMask.GetMask("Ground"));
        if (collision && !collision.isTrigger && !collision.CompareTag("Player"))
            return true;

        Vector3 ogWorldLocation = worldLocation;
        while (GetTileAtWorldLocation(worldLocation) is TileInteractionRelocator it)
            worldLocation += (Vector3) it.offset * 0.5f;

        Vector3Int tileLocation = WorldToTilemapPosition(worldLocation);
        Matrix4x4 transform = GameManager.Instance.tilemap.GetTransformMatrix(tileLocation);

        Sprite sprite = GameManager.Instance.tilemap.GetSprite(tileLocation);
        switch (GetColliderType(tileLocation)) {
        case Tile.ColliderType.Grid:
            return true;
        case Tile.ColliderType.None:
            return false;
        case Tile.ColliderType.Sprite:
            for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++) {
                List<Vector2> points = new();
                sprite.GetPhysicsShape(i, points);

                for (int j = 0; j < points.Count; j++)
                    points[i] = transform.MultiplyPoint3x4(points[i]);
                
                Vector2 tilePosition = TilemapToWorldPosition(WorldToTilemapPosition(worldLocation));
                if (IsInside(points.ToArray(), (Vector2) ogWorldLocation - tilePosition))
                    return true;
            }
            return false;
        }

        return IsTileSolidAtTileLocation(WorldToTilemapPosition(worldLocation));
    }

    // Given three collinear points p, q, r,
    // the function checks if point q lies
    // on line segment 'pr'
    static bool OnSegment(Vector2 p, Vector2 q, Vector2 r) {
        if (q.x <= Mathf.Max(p.x, r.x) &&
            q.x >= Mathf.Min(p.x, r.x) &&
            q.y <= Mathf.Max(p.y, r.y) &&
            q.y >= Mathf.Min(p.y, r.y)) {
            return true;
        }
        return false;
    }

    // To find orientation of ordered triplet (p, q, r).
    // The function returns following values
    // 0 --> p, q and r are collinear
    // 1 --> Clockwise
    // 2 --> Counterclockwise
    static float Orientation(Vector2 p, Vector2 q, Vector2 r) {
        float val = (q.y - p.y) * (r.x - q.x) -
                (q.x - p.x) * (r.y - q.y);

        if (Mathf.Abs(val) < 0.001f) {
            return 0; // collinear
        }
        return (val > 0) ? 1 : 2; // clock or counterclock wise
    }

    // The function that returns true if
    // line segment 'p1q1' and 'p2q2' intersect.
    static bool DoIntersect(Vector2 p1, Vector2 q1,
                            Vector2 p2, Vector2 q2) {
        // Find the four orientations needed for
        // general and special cases
        float o1 = Orientation(p1, q1, p2);
        float o2 = Orientation(p1, q1, q2);
        float o3 = Orientation(p2, q2, p1);
        float o4 = Orientation(p2, q2, q1);

        // General case
        if (o1 != o2 && o3 != o4) {
            return true;
        }

        // Special Cases
        // p1, q1 and p2 are collinear and
        // p2 lies on segment p1q1
        if (o1 == 0 && OnSegment(p1, p2, q1)) {
            return true;
        }

        // p1, q1 and p2 are collinear and
        // q2 lies on segment p1q1
        if (o2 == 0 && OnSegment(p1, q2, q1)) {
            return true;
        }

        // p2, q2 and p1 are collinear and
        // p1 lies on segment p2q2
        if (o3 == 0 && OnSegment(p2, p1, q2)) {
            return true;
        }

        // p2, q2 and q1 are collinear and
        // q1 lies on segment p2q2
        if (o4 == 0 && OnSegment(p2, q1, q2)) {
            return true;
        }

        // Doesn't fall in any of the above cases
        return false;
    }

    // Returns true if the point p lies
    // inside the polygon[] with n vertices
    static bool IsInside(Vector2[] polygon, Vector2 p) {
        // There must be at least 3 vertices in polygon[]
        if (polygon.Length < 3) {
            return false;
        }

        // Create a point for line segment from p to infinite
        Vector2 extreme = new(1000, p.y);

        // Count intersections of the above line
        // with sides of polygon
        int count = 0, i = 0;
        do {
            int next = (i + 1) % polygon.Length;

            // Check if the line segment from 'p' to
            // 'extreme' intersects with the line
            // segment from 'polygon[i]' to 'polygon[next]'
            if (DoIntersect(polygon[i],
                            polygon[next], p, extreme)) {
                // If the point 'p' is collinear with line
                // segment 'i-next', then check if it lies
                // on segment. If it lies, return true, otherwise false
                if (Orientation(polygon[i], p, polygon[next]) == 0) {
                    return OnSegment(polygon[i], p,
                                    polygon[next]);
                }
                count++;
            }
            i = next;
        } while (i != 0);

        // Return true if count is odd, false otherwise
        return (count % 2 == 1); // Same as (count%2 == 1)
    }

    public static bool IsAnyTileSolidBetweenWorldBox(Vector2 checkPosition, Vector2 checkSize) {
        Vector3Int minPos = WorldToTilemapPosition(checkPosition - (checkSize / 2), wrap: false);
        Vector3Int size = WorldToTilemapPosition(checkPosition + (checkSize / 2), wrap: false) - minPos;

        for (int x = 0; x <= size.x; x++) {
            for (int y = 0; y <= size.y; y++) {

                Vector3Int tileLocation = new(minPos.x + x, minPos.y + y, 0);
                WrapTileLocation(ref tileLocation);

                if (IsTileSolidAtTileLocation(tileLocation))
                    return true;
            }
        }
        return false;
    }

    public static float WrappedDistance(Vector2 a, Vector2 b) {
        if (GameManager.Instance && GameManager.Instance.loopingLevel && Mathf.Abs(a.x - b.x) > GameManager.Instance.levelWidthTile / 4f) 
            a.x -= GameManager.Instance.levelWidthTile / 2f * Mathf.Sign(a.x - b.x);

        return Vector2.Distance(a, b);
    }

    public static void GetCustomProperty<T>(string key, out T value, ExitGames.Client.Photon.Hashtable properties = null) {
        if (properties == null)
            properties = PhotonNetwork.CurrentRoom.CustomProperties;

        properties.TryGetValue(key, out object temp);
        if (temp != null) {
            value = (T) temp;
        } else {
            value = default;
        }
    }

    public static Powerup[] powerups = null;
    public static Powerup GetRandomItem(PlayerController player) {
        GameManager gm = GameManager.Instance;

        // "losing" variable based on ln(x+1), x being the # of stars we're behind (max being 5)
        int x = Mathf.Max(0, FirstPlaceStars - player.stars);
        float losing = Mathf.Log(x + 1, 2.71828f);

        if (powerups == null)
            powerups = Resources.LoadAll<Powerup>("Scriptables/Powerups");

        GetCustomProperty(Enums.NetRoomProperties.NewPowerups, out bool custom);
        bool big = gm.spawnBigPowerups;
        bool vertical = gm.spawnVerticalPowerups;

        float totalChance = 0;
        foreach (Powerup powerup in powerups) {
            if (powerup.name == "MegaMushroom" && GameManager.Instance.musicState == Enums.MusicState.MegaMushroom)
                continue;
            if ((powerup.big && !big) || (powerup.vertical && !vertical) || (powerup.custom && !custom))
                continue;

            totalChance += powerup.GetModifiedChance(losing);
        }

        float rand = Random.value * totalChance;
        foreach (Powerup powerup in powerups) {
            if (powerup.name == "MegaMushroom" && gm.musicState == Enums.MusicState.MegaMushroom)
                continue;
            if ((powerup.big && !big) || (powerup.vertical && !vertical) || (powerup.custom && !custom))
                continue;

            float chance = powerup.GetModifiedChance(losing);

            if (rand < chance) 
                return powerup;
            rand -= chance;
        }

        return powerups[0];
    }
    
    public static float QuadraticEaseOut(float v) {
        return -1 * v * (v - 2);
    }
    

    public static ExitGames.Client.Photon.Hashtable GetTilemapChanges(TileBase[] original, BoundsInt bounds, Tilemap tilemap) {
        Dictionary<int, int> changes = new();
        List<string> tiles = new();

        TileBase[] current = tilemap.GetTilesBlock(bounds);

        for (int i = 0; i < original.Length; i++) {
            if (current[i] == original[i])
                continue;

            TileBase cTile = current[i];
            string path;
            if (cTile == null) {
                path = "";
            } else {
                path = ResourceDB.GetAsset(cTile.name).ResourcesPath;
            }

            if (!tiles.Contains(path))
                tiles.Add(path);
            
            changes[i] = tiles.IndexOf(path);
        }

        return new() {
            ["T"] = tiles.ToArray(),
            ["C"] = changes,
        };
    }

    public static void ApplyTilemapChanges(TileBase[] original, BoundsInt bounds, Tilemap tilemap, ExitGames.Client.Photon.Hashtable changesTable) {
        TileBase[] copy = (TileBase[]) original.Clone();

        Dictionary<int, int> changes = (Dictionary<int, int>) changesTable["C"];
        string[] tiles = (string[]) changesTable["T"];

        foreach (KeyValuePair<int, int> pairs in changes) {
            copy[pairs.Key] = GetTileFromCache(tiles[pairs.Value]);
        }

        tilemap.SetTilesBlock(bounds, copy);
    }

    private static readonly Dictionary<string, TileBase> tileCache = new();
    public static TileBase GetTileFromCache(string tilename) {
        if (tilename == null || tilename == "")
            return null;
        
        if (!tilename.StartsWith("Tilemaps/Tiles/"))
            tilename = "Tilemaps/Tiles/" + tilename;

        return tileCache.ContainsKey(tilename) ?
            tileCache[tilename] :
            tileCache[tilename] = Resources.Load(tilename) as TileBase;
    }

    private static readonly Dictionary<char, int> charToSymbolIndex = new() {
        ['c'] = 6,
        ['0'] = 11,
        ['1'] = 12,
        ['2'] = 13,
        ['3'] = 14,
        ['4'] = 15,
        ['5'] = 16,
        ['6'] = 17,
        ['7'] = 18,
        ['8'] = 19,
        ['9'] = 20,
        ['x'] = 21,
        ['C'] = 22,
        ['S'] = 23,
        ['/'] = 24,
        [':'] = 25,
    };
    public static string GetSymbolString(string str) {
        string ret = "";
        foreach (char c in str) {
            if (charToSymbolIndex.ContainsKey(c)) {
                ret += "<sprite=" + charToSymbolIndex[c] + ">";
            } else {
                ret += c;
            }
        }
        return ret;
    }

    public static Color GetPlayerColor(Player player, float s = 1, float v = 1) {
        int id = System.Array.IndexOf(PhotonNetwork.PlayerList, player);
        return Color.HSVToRGB(id / ((float) PhotonNetwork.PlayerList.Length + 1), s, v);
    }

    public static void TickTimer(ref float counter, float min, float delta, float max = float.MaxValue) {
        counter = Mathf.Clamp(counter - delta, min, max);
    }
}