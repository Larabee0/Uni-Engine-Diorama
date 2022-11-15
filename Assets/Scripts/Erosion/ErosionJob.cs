using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

/// <summary>
/// Original Alogirthim Designed for 1 square height map at once
/// Converted from Compute shader to run in C# Jobs.
/// </summary>
[BurstCompile]
public struct ErodeJob : IJobParallelFor
{
    public ErodeSettings settings;

    [ReadOnly]
    public NativeList<int> brushIndexOffsets;
    [ReadOnly]
    public NativeList<float> brushWeights;
    [NativeDisableParallelForRestriction]
    public NativeArray<HeightMapElement> heightMap;

    public void Execute(int iteration)
    {
        Random prng = new(settings.baseSeed + (uint)iteration);
        int2 min = new(settings.erosionBrushRadius);
        int2 max = new()
        {
            x = settings.mapSize.x + settings.erosionBrushRadius,
            y = settings.mapSize.y + settings.erosionBrushRadius
        };

        int2 random = prng.NextInt2(min, max);
        int index = random.y * settings.mapSize.x + random.x;

        float posX = (float)index % settings.mapSizeWithBorder.x;
        float posY = (float)index / settings.mapSizeWithBorder.x;
        float dirX = 0;
        float dirY = 0;
        float speed = settings.startSpeed;
        float water = settings.startWater;
        float sediment = 0;

        for (int lifetime = 0; lifetime < settings.maxLifetime; lifetime++)
        {
            int nodeX = (int)posX;
            int nodeY = (int)posY;
            int dropletIndex = nodeY * settings.mapSizeWithBorder.x + nodeX;
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
            if ((dirX == 0 && dirY == 0) || 
                posX < settings.erosionBrushRadius || 
                posX > settings.mapSizeWithBorder.x - settings.erosionBrushRadius || 
                posY < settings.erosionBrushRadius || 
                posY > settings.mapSizeWithBorder.y - settings.erosionBrushRadius)
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
                HeightMapElement dropletElement = heightMap[dropletIndex];
                HeightMapElement dropletElementPlusOne = heightMap[dropletIndex + 1];
                HeightMapElement dropletElementBorder = heightMap[dropletIndex + settings.mapSizeWithBorder.x];
                HeightMapElement dropletElementBorderPlusOne = heightMap[dropletIndex + settings.mapSizeWithBorder.x + 1];

                dropletElement.Value += amountToDeposit * (1 - cellOffsetX) * (1 - cellOffsetY);
                dropletElementPlusOne.Value += amountToDeposit * cellOffsetX * (1 - cellOffsetY);
                dropletElementBorder.Value += amountToDeposit * (1 - cellOffsetX) * cellOffsetY;
                dropletElementBorderPlusOne.Value += amountToDeposit * cellOffsetX * cellOffsetY;

                heightMap[dropletIndex] = dropletElement;
                heightMap[dropletIndex + 1] = dropletElementPlusOne;
                heightMap[dropletIndex + settings.mapSizeWithBorder.x] = dropletElementBorder;
                heightMap[dropletIndex + settings.mapSizeWithBorder.x + 1] = dropletElementBorderPlusOne;
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
                    float deltaSediment = (heightMap[erodeIndex].Value < weightedErodeAmount) ? heightMap[erodeIndex].Value : weightedErodeAmount;
                    HeightMapElement erodeIndexElement = heightMap[erodeIndex];
                    erodeIndexElement.Value -= deltaSediment;
                    heightMap[erodeIndex] = erodeIndexElement;
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
        int nodeIndexNW = coordY * settings.mapSizeWithBorder.x + coordX;
        float heightNW = heightMap[nodeIndexNW].Value;
        float heightNE = heightMap[nodeIndexNW + 1].Value;
        float heightSW = heightMap[nodeIndexNW + settings.mapSizeWithBorder.x].Value;
        float heightSE = heightMap[nodeIndexNW + settings.mapSizeWithBorder.x + 1].Value;

        // Calculate droplet's direction of flow with bilinear interpolation of height difference along the edges
        float gradientX = (heightNE - heightNW) * (1 - y) + (heightSE - heightSW) * y;
        float gradientY = (heightSW - heightNW) * (1 - x) + (heightSE - heightNE) * x;

        // Calculate height with bilinear interpolation of the heights of the nodes of the cell
        float height = heightNW * (1 - x) * (1 - y) + heightNE * x * (1 - y) + heightSW * (1 - x) * y + heightSE * x * y;

        return new float3(gradientX, gradientY, height);
    }
}


/// <summary>
/// Majorly modified alogirthim designed for multuple height maps at once, supporting none cubic sizes.
/// </summary>
[BurstCompile]
public struct BigErodeJob : IJobParallelFor
{
    public MeshAreaSettings mapSettings;

    [ReadOnly]
    public NativeArray<NoiseSettings> erosionSettings;
    [ReadOnly,DeallocateOnJobCompletion]
    public NativeArray<int2> brushRanges;
    [ReadOnly,DeallocateOnJobCompletion]
    public NativeArray<int> brushIndexOffsets;
    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<float> brushWeights;

    [ReadOnly,DeallocateOnJobCompletion]
    public NativeArray<RandomErosionElement> randomIndices;

    [NativeDisableParallelForRestriction]
    public NativeArray<HeightMapElement> heightMap;

    public void Execute(int iteration)
    {
        RandomErosionElement rEE = randomIndices[iteration];
        ErodeSettings settings = erosionSettings[rEE.layerIndex].erosionSettings;
        NativeArray<int> brushIndexOffsetsInternal = brushIndexOffsets.GetSubArray(brushRanges[rEE.layerIndex].x, brushRanges[rEE.layerIndex].y);
        NativeArray<float> brushWeightsInternal = brushWeights.GetSubArray(brushRanges[rEE.layerIndex].x, brushRanges[rEE.layerIndex].y);

        int localIndexOffset = rEE.layerIndex * settings.mapSizeWithBorder.x;

        int elementIndex = rEE.heightMapElementIndex;
        float posX = (float)(elementIndex - localIndexOffset) % settings.mapSizeWithBorder.x;
        float posY = (float)(elementIndex - localIndexOffset) / settings.mapSizeWithBorder.x;
        float dirX = 0;
        float dirY = 0;
        float speed = settings.startSpeed;
        float water = settings.startWater;
        float sediment = 0;

        for (int lifeTime = 0; lifeTime < settings.maxLifetime; lifeTime++)
        {
            int nodeX = (int)posX;
            int nodeY = (int)posY;
            int dropletIndex = nodeY * settings.mapSizeWithBorder.x + nodeX;

            float cellOffsetX = posX - nodeX;
            float cellOffsetY = posY - nodeY;

            float3 heightAndGradient = CalculateHeightAndGradient(settings.mapSizeWithBorder.x, localIndexOffset, posX, posY);

            dirX = dirX * settings.inertia - heightAndGradient.x * (1 - settings.inertia);
            dirY = dirY * settings.inertia - heightAndGradient.y * (1 - settings.inertia);

            float len = math.max(0.01f, math.sqrt(dirX * dirX + dirY * dirY));
            dirX /= len;
            dirY /= len;
            posX += dirX;
            posY += dirY;
            if ((dirX == 0 && dirY == 0) || posX < settings.erosionBrushRadius || posX > mapSettings.mapDimentions.x+settings.erosionBrushRadius || posY < settings.erosionBrushRadius || posY > mapSettings.mapDimentions.y+settings.erosionBrushRadius)
            {
                break;
            }


            float newHeight = CalculateHeightAndGradient(settings.mapSizeWithBorder.x, localIndexOffset, posX, posY).z;
            float deltaHeight = newHeight - heightAndGradient.z;

            float sedimentCapacity = math.max(-deltaHeight * speed * water * settings.sedimentCapacityFactor, settings.minSedimentCapacity);

            if (sediment > sedimentCapacity || deltaHeight > 0)
            {
                float amountToDeposit = (deltaHeight > 0) ? math.min(deltaHeight, sediment) : (sediment - sedimentCapacity) * settings.depositSpeed;
                sediment -= amountToDeposit;
                dropletIndex += localIndexOffset;

                HeightMapElement e0 = heightMap[dropletIndex];
                HeightMapElement e1 = heightMap[dropletIndex + 1];
                HeightMapElement e2 = heightMap[dropletIndex+ settings.mapSizeWithBorder.x];
                HeightMapElement e3 = heightMap[dropletIndex + settings.mapSizeWithBorder.x + 1];
                e0.Value += amountToDeposit * (1 - cellOffsetX) * (1 - cellOffsetY);
                e1.Value += amountToDeposit * cellOffsetX * (1 - cellOffsetY);
                e2.Value += amountToDeposit * (1 - cellOffsetX) * cellOffsetY;
                e3.Value += amountToDeposit * cellOffsetX * cellOffsetY;
                heightMap[dropletIndex] = e0;
                heightMap[dropletIndex + 1] = e1;
                heightMap[dropletIndex + settings.mapSizeWithBorder.x] = e2;
                heightMap[dropletIndex + settings.mapSizeWithBorder.x + 1 ]= e3;
            }
            else
            {
                float amountToErode = math.min((sedimentCapacity - sediment) * settings.erodeSpeed, -deltaHeight);

                for (int i = 0; i < brushIndexOffsetsInternal.Length; i++)
                {
                    int erodeIndex = dropletIndex + brushIndexOffsetsInternal[i] + localIndexOffset;
                    if(erodeIndex < 0)
                    {
                        continue;
                    }
                    float weightedErodeAmount = amountToErode * brushWeightsInternal[i];
                    float deltaSediment = (heightMap[erodeIndex].Value < weightedErodeAmount) ? heightMap[erodeIndex].Value : weightedErodeAmount;

                    HeightMapElement element = heightMap[erodeIndex];
                    element.Value -= deltaSediment;
                    heightMap[erodeIndex] = element;

                    sediment += deltaSediment;
                }
            }

            speed = math.sqrt(math.max(0, speed * speed + deltaHeight * settings.gravity));
            water *= (1 - settings.evaporateSpeed);
        }

    }

    private float3 CalculateHeightAndGradient(int fullSizeWidth,int localIndexOffset, float posX, float posY)
    {
        int coordX = (int)posX;
        int coordY = (int)posY;

        float x = posX - coordX;
        float y = posY - coordY;

        int nodeIndexNW = coordY * fullSizeWidth + coordX;
        nodeIndexNW += localIndexOffset;
        float heightNW = heightMap[nodeIndexNW].Value;

        float heightNE = heightMap[nodeIndexNW + 1].Value;
        float heightSW = heightMap[nodeIndexNW + fullSizeWidth].Value;
        float heightSE = heightMap[nodeIndexNW + fullSizeWidth + 1].Value;

        float gradientX = (heightNE - heightNW) * (1 - y) + (heightSE - heightSW) * y;
        float gradientY = (heightSW - heightNW) * (1 - x) + (heightSE - heightNE) * x;

        float height = heightNW * (1 - x) * (1 - y) + heightNE * x * (1 - y) + heightSW * (1 - x) * y + heightSE * x * y;

        return new float3(gradientX, gradientY, height);
    }
}

[BurstCompile]
public struct RandomIndexGenerator : IJobParallelFor
{
    public MeshAreaSettings mapSettings;
    [ReadOnly]
    public NativeArray<NoiseSettings> eroisonSettings;
    [ReadOnly,DeallocateOnJobCompletion]
    public NativeArray<int> layerStartIndices;
    public NativeArray<RandomErosionElement> randomIndices;


    public void Execute(int index)
    {
        int layerIndex = 0;
        for (; layerIndex < layerStartIndices.Length; layerIndex++)
        {
            if(index >= layerStartIndices[layerIndex])
            {
                break;
            }
        }
        ErodeSettings settings = eroisonSettings[layerIndex].erosionSettings;

        Random prng = new(settings.baseSeed + ((uint)index - (uint)layerStartIndices[layerIndex]));
        int2 min = new(settings.erosionBrushRadius);
        int2 max = new()
        {
            x = mapSettings.mapDimentions.x + settings.erosionBrushRadius,
            y = mapSettings.mapDimentions.y + settings.erosionBrushRadius
        };
        int2 randomXY = prng.NextInt2(min, max);
        randomIndices[index] =new( randomXY.y * mapSettings.mapDimentions.x +randomXY.x,layerIndex);
    }
}

[BurstCompile]
public struct ErosionCombiner : IJobFor
{

    public MeshAreaSettings mapSettings;

    [ReadOnly]
    public NativeArray<NoiseSettings> noiseSettings;
    [ReadOnly,DeallocateOnJobCompletion]
    public NativeArray<int> erosionSettingsIndexRemap;

    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HeightMapElement> source;
    [WriteOnly]
    public NativeArray<HeightMapElement> destination;

    public void Execute(int index)
    {
        int mapArrayLength = mapSettings.mapDimentions.x * mapSettings.mapDimentions.y;
        int layerIndex = index / mapArrayLength;
        if (noiseSettings[layerIndex].erosionSettings.erosion)
        {
            ErodeSettings settings = noiseSettings[layerIndex].erosionSettings;

            int localOffset = layerIndex * mapArrayLength;
            int2 dstXY = new()
            {
                x = (index - localOffset) % mapSettings.mapDimentions.x,
                y = (index - localOffset) / mapSettings.mapDimentions.x
            };

            int widthWithBorder = mapSettings.mapDimentions.x + settings.erosionBrushRadius * 2;

            int sourceIndex = (dstXY.y + settings.erosionBrushRadius) * widthWithBorder + dstXY.x +  settings.erosionBrushRadius;
            sourceIndex += erosionSettingsIndexRemap[layerIndex] * settings.mapSizeWithBorder.x;
            destination[index] = source[sourceIndex];
        }
    }
}

public struct RandomErosionElement
{
    public int heightMapElementIndex;
    public int layerIndex;

    public RandomErosionElement(int heightMapElementIndex, int layerIndex)
    {
        this.heightMapElementIndex = heightMapElementIndex;
        this.layerIndex = layerIndex;

    }
}


[BurstCompile]
public struct ErosionCutter : IJobFor
{
    public MeshAreaSettings mapSettings;

    public ErodeSettings eroisonSettings;

    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HeightMapElement> source;
    [WriteOnly]
    public NativeArray<HeightMapElement> destination;

    public void Execute(int index)
    {
        int mapArrayLength = mapSettings.mapDimentions.x * mapSettings.mapDimentions.y;
        int settingsIndex = index / mapArrayLength;
        int2 dstXY = new()
        {
            x = index % mapSettings.mapDimentions.x,
            y = index  / mapSettings.mapDimentions.x
        };
        int widthWithBorder = mapSettings.mapDimentions.x + eroisonSettings.erosionBrushRadius * 2;

        int sourceIndex = (dstXY.y + eroisonSettings.erosionBrushRadius) * widthWithBorder + dstXY.x + eroisonSettings.erosionBrushRadius;

        sourceIndex += settingsIndex * eroisonSettings.mapSizeWithBorder.x;
        destination[index] = source[sourceIndex];
    }
}
