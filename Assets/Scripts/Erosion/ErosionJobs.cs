using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

[BurstCompile]
public struct ErodeJob : IJobParallelFor
{
    public ErodeSettings settings;

    [ReadOnly]
    public NativeList<int> brushIndexOffsets;
    [ReadOnly]
    public NativeParallelHashSet<int> possibleTargets;
    [ReadOnly]
    public NativeList<float> brushWeights;
    [NativeDisableParallelForRestriction]
    public NativeArray<HeightMapElement> map;

    public void Execute(int iteration)
    {
        Random prng = new(settings.baseSeed + (uint)iteration);

        int2 random = prng.NextInt2(settings.erosionBrushRadius, settings.mapSize + settings.erosionBrushRadius);
        int index = random.y * settings.mapSize + random.x;
        while (!possibleTargets.Contains(index))
        {
            random = prng.NextInt2(settings.erosionBrushRadius, settings.mapSize + settings.erosionBrushRadius);
            index = random.y * settings.mapSize + random.x;
        }
        float posX = (float)index % settings.mapSizeWithBorder;
        float posY = (float)index / settings.mapSizeWithBorder;
        float dirX = 0;
        float dirY = 0;
        float speed = settings.startSpeed;
        float water = settings.startWater;
        float sediment = 0;

        for (int lifetime = 0; lifetime < settings.maxLifetime; lifetime++)
        {
            int nodeX = (int)posX;
            int nodeY = (int)posY;
            int dropletIndex = nodeY * settings.mapSizeWithBorder + nodeX;
            // Calculate droplet's offset inside the cell (0,0) = at NW node, (1,1) = at SE node
            float cellOffsetX = posX - nodeX;
            float cellOffsetY = posY - nodeY;

            // Calculate droplet's height and direction of flow with bilinear interpolation of surrounding heights
            float3 heightAndGradient = CalculateHeightAndGradient(posX, posY);

            // Update the droplet's direction and position (move position 1 unit regardless of speed)
            dirX = (dirX * settings.inertia - heightAndGradient.x * (1 - settings.inertia));
            dirY = (dirY * settings.inertia - heightAndGradient.y * (1 - settings.inertia));
            // Normalize direction
            float len = math.max(0.01f, math.sqrt(dirX * dirX + dirY * dirY));
            dirX /= len;
            dirY /= len;
            posX += dirX;
            posY += dirY;

            // Stop simulating droplet if it's not moving or has flowed over edge of map
            if ((dirX == 0 && dirY == 0) || posX < settings.erosionBrushRadius || posX > settings.mapSizeWithBorder - settings.erosionBrushRadius || posY < settings.erosionBrushRadius || posY > settings.mapSizeWithBorder - settings.erosionBrushRadius)
            {
                break;
            }

            // Find the droplet's new height and calculate the deltaHeight
            float newHeight = CalculateHeightAndGradient(posX, posY).z;
            float deltaHeight = newHeight - heightAndGradient.z;

            // Calculate the droplet's sediment capacity (higher when moving fast down a slope and contains lots of water)
            float sedimentCapacity = math.max(-deltaHeight * speed * water * settings.sedimentCapacityFactor, settings.minSedimentCapacity);
            
            // If carrying more sediment than capacity, or if flowing uphill:
            if (sediment > sedimentCapacity || deltaHeight > 0)
            {
                // If moving uphill (deltaHeight > 0) try fill up to the current height, otherwise deposit a fraction of the excess sediment
                float amountToDeposit = (deltaHeight > 0) ? math.min(deltaHeight, sediment) : (sediment - sedimentCapacity) * settings.depositSpeed;
                sediment -= amountToDeposit;

                // Add the sediment to the four nodes of the current cell using bilinear interpolation
                // Deposition is not distributed over a radius (like erosion) so that it can fill small pits
                HeightMapElement dropletElement = map[dropletIndex];
                HeightMapElement dropletElementPlusOne = map[dropletIndex + 1];
                HeightMapElement dropletElementBorder = map[dropletIndex + settings.mapSizeWithBorder];
                HeightMapElement dropletElementBorderPlusOne = map[dropletIndex + settings.mapSizeWithBorder + 1];

                dropletElement.Value += amountToDeposit * (1 - cellOffsetX) * (1 - cellOffsetY);
                dropletElementPlusOne.Value += amountToDeposit * cellOffsetX * (1 - cellOffsetY);
                dropletElementBorder.Value += amountToDeposit * (1 - cellOffsetX) * cellOffsetY;
                dropletElementBorderPlusOne.Value += amountToDeposit * cellOffsetX * cellOffsetY;

                map[dropletIndex] = dropletElement;
                map[dropletIndex + 1] = dropletElementPlusOne;
                map[dropletIndex + settings.mapSizeWithBorder] = dropletElementBorder;
                map[dropletIndex + settings.mapSizeWithBorder + 1] = dropletElementBorderPlusOne;
            }
            else
            {
                // Erode a fraction of the droplet's current carry capacity.
                // Clamp the erosion to the change in height so that it doesn't dig a hole in the terrain behind the droplet
                float amountToErode = math.min((sedimentCapacity - sediment) * settings.erodeSpeed, -deltaHeight);

                for (int i = 0; i < brushIndexOffsets.Length; i++)
                {
                    int erodeIndex = dropletIndex + brushIndexOffsets[i];

                    float weightedErodeAmount = amountToErode * brushWeights[i];
                    float deltaSediment = (map[erodeIndex].Value < weightedErodeAmount) ? map[erodeIndex].Value : weightedErodeAmount;
                    HeightMapElement erodeIndexElement = map[erodeIndex];
                    erodeIndexElement.Value -= deltaSediment;
                    map[erodeIndex] = erodeIndexElement;
                    sediment += deltaSediment;
                }
            }

            // Update droplet's speed and water content
            speed = math.sqrt(math.max(0, speed * speed + deltaHeight * settings.gravity));
            water *= (1 - settings.evaporateSpeed);
        }
    }

    private float3 CalculateHeightAndGradient(float posX, float posY)
    {
        int coordX = (int)posX;
        int coordY = (int)posY;

        // Calculate droplet's offset inside the cell (0,0) = at NW node, (1,1) = at SE node
        float x = posX - coordX;
        float y = posY - coordY;

        // Calculate heights of the four nodes of the droplet's cell
        int nodeIndexNW = coordY * settings.mapSizeWithBorder + coordX;
        float heightNW = map[nodeIndexNW].Value;
        float heightNE = map[nodeIndexNW + 1].Value;
        float heightSW = map[nodeIndexNW + settings.mapSizeWithBorder].Value;
        float heightSE = map[nodeIndexNW + settings.mapSizeWithBorder + 1].Value;

        // Calculate droplet's direction of flow with bilinear interpolation of height difference along the edges
        float gradientX = (heightNE - heightNW) * (1 - y) + (heightSE - heightSW) * y;
        float gradientY = (heightSW - heightNW) * (1 - x) + (heightSE - heightNE) * x;

        // Calculate height with bilinear interpolation of the heights of the nodes of the cell
        float height = heightNW * (1 - x) * (1 - y) + heightNE * x * (1 - y) + heightSW * (1 - x) * y + heightSE * x * y;

        return new float3(gradientX, gradientY, height);
    }
}