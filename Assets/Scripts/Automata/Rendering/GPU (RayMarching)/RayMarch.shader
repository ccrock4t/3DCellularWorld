/*
* Ray Marching shader
* https://www.youtube.com/watch?v=S8AWd66hoCo
*/
Shader "Unlit/RayMarch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque"}
        LOD 100



        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct CellInfo{
                int current_state;
                int next_state;
                int last_frame_modified;
            };
            
#define MAX_STEPS 10000
#define MAX_DIST 10000
#define SURF_DIST 1e-3
#define ELEMENT_EMPTY 0
#define ELEMENT_STONE 1
#define ELEMENT_SAND 2
#define ELEMENT_WATER 3
#define ELEMENT_LAVA 4
#define ELEMENT_STEAM 5


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f // vertex to fragment
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 camera_origin : TEXCOORD1;
                float3 hit_position : TEXCOORD2;
                float3 light_position : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            StructuredBuffer<CellInfo> _voxel_data;
            int _voxel_data_length;
            int AUTOMATA_SIZE_X;
            int AUTOMATA_SIZE_Y;
            int AUTOMATA_SIZE_Z;
            float light_position_X;
            float light_position_Y;
            float light_position_Z;
            int3 step;
            float tMaxX;
            float tMaxY;
            float tMaxZ;
            float tDeltaX;
            float tDeltaY;
            float tDeltaZ;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // object space
                //o.ray_origin = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1)); 
                //o.hit_position = v.vertex; 
                //world space
                o.camera_origin = _WorldSpaceCameraPos; 
                o.light_position = float3(light_position_X,light_position_Y, light_position_Z);
                o.hit_position = mul(unity_ObjectToWorld, v.vertex); 
                return o;
            }

            int FlatIndexFromCoordinates(int x, int y, int z)
            {
                return x + AUTOMATA_SIZE_X * y + AUTOMATA_SIZE_X * AUTOMATA_SIZE_Y * z;
            }

            int FlatIndexFromVector3(int3 index)
            {
                return FlatIndexFromCoordinates(index.x, index.y, index.z);
            }

            float4 GetVoxelColor(int element){

                if(element == ELEMENT_STONE){
                    return float4(0.3568628,0.3568628,0.3568628,1.0);
                }else if(element == ELEMENT_SAND){
                    return float4(0.9352227,1,0.4669811,1.0);
                }else if(element == ELEMENT_WATER){
                    return float4(0, 0.170235,0.9716981,0.5686275);
                }else if(element == ELEMENT_LAVA){
                    return float4(1, 0.1527778,0,1.0);
                }else if(element == ELEMENT_STEAM){
                    return float4(0, 0.919493,1,0.1254902);
                }else{
                    return float4(0,0,0,0);
                }
            }

            bool IsOutOfAutomataBounds(int3 index){
                return (index.x < 0 || index.x >= AUTOMATA_SIZE_X || index.y < 0 || index.y >= AUTOMATA_SIZE_Y || index.z < 0 || index.z >= AUTOMATA_SIZE_Z);
            }

            bool IsGas(int element){
                if(element == ELEMENT_STEAM) return true;
                return false;
            }
  

          //  float3 GetNormal(float3 p){
          //      float2 e = float2(1e-2, 0);
         //       float3 n = GetDist(p) - float3(
          //          GetDist(p - e.xyy),
        //            GetDist(p - e.yxy),
         //           GetDist(p - e.yyx)
          //          );
          //      return normalize(n);
          //  }

            void VoxelMarchInitialization(float3 ray_origin, float3 ray_direction){

                //the direction of a voxel step, according to the raycast direction
                step.x = 0;
                if(ray_direction.x > 0){
                    step.x = 1;
                }else if(ray_direction.x < 0){
                    step.x = -1;
                }else if(ray_direction.x == 0){
                    step.x = 0;
                }

                step.y = 0;
                 if(ray_direction.y > 0){
                    step.y = 1;
                }else if(ray_direction.y < 0){
                    step.y = -1;
                }else if(ray_direction.z == 0){
                    step.y = 0;
                }

                step.z = 0;
                if(ray_direction.z > 0){
                    step.z = 1;
                }else if(ray_direction.z < 0){
                    step.z = -1;
                }else if(ray_direction.z == 0){
                    step.z = 0;
                }


                // the origin voxel
               int voxelX = (int)ray_origin.x;
               int voxelY = (int)ray_origin.y;
               int voxelZ = (int)ray_origin.z;
               //difference between point and voxel border
               float diffX = -1 * step.x * ray_origin.x + step.x * voxelX + ((1+step.x)/2.0); 
               float diffY = -1 * step.y * ray_origin.y + step.y * voxelY + ((1+step.y)/2.0);
               float diffZ = -1 * step.z * ray_origin.z + step.z * voxelZ + ((1+step.z)/2.0);

               // magnitude of distance, how far along the ray must be traversed to traverse 1 voxel in a given dimension
               tDeltaX = abs(1 / ray_direction.x);
               tDeltaY = abs(1 / ray_direction.y); 
               tDeltaZ = abs(1 / ray_direction.z);

               // These store the overall distance travelled to the current hit voxel in each dimension
               tMaxX = tDeltaX*diffX;
               tMaxY = tDeltaY*diffY;
               tMaxZ = tDeltaZ*diffZ;

            }

            int3 NextVoxel(int X, int Y, int Z){
                   if(tMaxX < tMaxY) {
                        if(tMaxX < tMaxZ) {
                            X = X + step.x;
                            tMaxX= tMaxX + tDeltaX; // traverse to next X voxel
                        } else {
                            Z = Z + step.z;
                            tMaxZ= tMaxZ + tDeltaZ; // traverse to next Z voxel
                        }
                    } else {
                        if(tMaxY < tMaxZ) {
                            Y = Y + step.y;
                            tMaxY= tMaxY + tDeltaY; // traverse to next Y voxel
                        } else {
                            Z = Z + step.z;
                            tMaxZ= tMaxZ + tDeltaZ; // traverse to next Z voxel
                        }
                    }

                    return int3(X, Y, Z);
                }

             int3 NextVoxel(int3 index){
                return NextVoxel(index[0], index[1], index[2]);
             }

            // returns -1 if no non-empty voxel hit, returns the index of the hit voxel otherwise
            int3 VoxelMarch(int X, int Y,int Z){
                int start_flat = FlatIndexFromCoordinates(X,Y,Z);
                int z = 0;
                int flat_voxel_index;
                do{
                    int3 next_index = NextVoxel(X,Y,Z);
                    X = next_index[0];
                    Y = next_index[1];
                    Z = next_index[2];
                    if(X < 0 || X >= AUTOMATA_SIZE_X || Y < 0 || Y >= AUTOMATA_SIZE_Y || Z < 0 || Z >= AUTOMATA_SIZE_Z){
                        flat_voxel_index = -1;
                        break;
                    }
                    flat_voxel_index = FlatIndexFromCoordinates(X,Y,Z);

                    z++;
                }while(_voxel_data[flat_voxel_index].next_state == ELEMENT_EMPTY && z < MAX_STEPS);
                if(z >= MAX_STEPS || _voxel_data[flat_voxel_index].next_state == ELEMENT_EMPTY){
                    flat_voxel_index = -1;
                    }

                if(flat_voxel_index == -1) return int3(-1,-1,-1);
                return int3(X, Y, Z);
            }



            float4 BlendColors(float4 col1, float4 col2){
                return lerp(col1, col2, 0.5);
            }


            // main function
            fixed4 frag (v2f i) : SV_Target
            {

                float3 ray_origin = i.camera_origin;
                float3 ray_direction = normalize(i.hit_position - ray_origin); // from camera center to outward ray

                VoxelMarchInitialization(ray_origin, ray_direction);
                int3 voxel_index = VoxelMarch((int)ray_origin.x, (int)ray_origin.y, (int)ray_origin.z);


                if(IsOutOfAutomataBounds(voxel_index)){
                    discard;
                }

                // surface was hit
                
                float3 light_position = float3(light_position_X, light_position_Y, light_position_Z);
                int flat_voxel_index = FlatIndexFromVector3(voxel_index);

                // draw pixel to texture
                int element = _voxel_data[flat_voxel_index].next_state;
                float4 rgba = GetVoxelColor(element);

                // make gas transparent
                if(IsGas(element)){
                    // keep marching to next voxel

                    ray_origin = NextVoxel(voxel_index);
                    

                    int3 behind_voxel_index = VoxelMarch(ray_origin.x, ray_origin.y, ray_origin.z);
                    int flat_behind_voxel_index = FlatIndexFromVector3(behind_voxel_index);
                    if(!IsOutOfAutomataBounds(voxel_index)){
                        int behind_element = _voxel_data[flat_behind_voxel_index].next_state;
                        rgba = BlendColors(rgba,GetVoxelColor(behind_element));
                    }
                }else{
   
                    //march to shadow
                    ray_origin = voxel_index;
                    ray_direction = normalize(light_position - ray_origin); // from origin to light position

                    VoxelMarchInitialization(ray_origin, ray_direction);
                    ray_origin = NextVoxel(voxel_index);
                    ray_origin = NextVoxel(ray_origin);
                    int3 light_voxel_index = VoxelMarch((int)ray_origin.x, (int)ray_origin.y, (int)ray_origin.z);

                    if(light_voxel_index[0] != -1){ // voxel was hit
                        int light_voxel_flat_index  = FlatIndexFromVector3(light_voxel_index);
                        bool same_voxel = (light_voxel_flat_index == flat_voxel_index);
                        if(!same_voxel){
                            rgba[0] *= 0.5; // shade color
                            rgba[1] *= 0.5; // shade color
                            rgba[2] *= 0.5; // shade color
               
                        }

                    }
                    //rgba = float4(abs(ray_direction.x),abs(ray_direction.y),abs(ray_direction.z),1);
                    //rgba = float4(abs(light_position.x - ,ray_direction.y,ray_direction.z,1);
                    // rgba = float4(ray_origin[0] / (float)AUTOMATA_SIZE_X, ray_origin[1] / (float)AUTOMATA_SIZE_Y, ray_origin[2] / (float)AUTOMATA_SIZE_Z,1);
 
                }
                    
                fixed4 col = 0;
                col.rgba = rgba;
              
                
                return col;
            }
            ENDCG
        }
    }
}
