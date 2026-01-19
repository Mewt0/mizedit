-- Label parameters
-- Copyright (C) 2018, Eagle Dynamics.


-- 标签格式
-- labels =  0  -- NONE  -- 无标签
-- labels =  1  -- FULL  -- 完整标签
-- labels =  2  -- ABBREVIATED  -- 简写标签
-- labels =  3  -- DOT ONLY  -- 仅点状

-- Off: No labels are used
-- Full: As we have now
-- Abbreviated: Only red or blue dot and unit type name based on side
-- Dot Only: Only red or blue dot based on unit side



local IS_DOT 		 = labels and labels ==  3
local IS_ABBREVIATED = labels and labels ==  2

AirOn			 		= true
GroundOn 		 		= true
NavyOn		 	 		= true
WeaponOn 		 		= true
labels_format_version 	= 1 -- labels format vesrion  标签格式版本（1 = 完整）
---------------------------------
-- 标签文本格式符号
-- %N - 型号
-- %D - 到对象的距离
-- %P - 对象名称（飞行员名）
-- %n - 另起一行
-- %% - 符号 '%'
-- %x, where x is not NDPn% - symbol 'x'
-- %C - extended info for vehicle's and ship's weapon systems
------------------------------------------
-- 举例
-- labelFormat[5000] = {"Name: %N%nDistance: %D%n Pilot: %P","LeftBottom",0,0}
-- up to 5km label is:
--       Name: Su-33
--       Distance: 30km
--       Pilot: Pilot1


-- 对齐选项 
--"RightBottom" -- 右下
--"LeftTop"  -- 右上
--"RightTop"  -- 右上
--"LeftCenter"  -- 左中
--"RightCenter"  -- 右中
--"CenterBottom"  -- 底中
--"CenterTop"  -- 顶中
--"CenterCenter"  -- 正中
--"LeftBottom"  -- 左下


-- labels font properties {font_file_name, font_size_in_pixels, text_shadow_offset_x, text_shadow_offset_y, text_blur_type}
-- text_blur_type = 0 - none
-- text_blur_type = 1 - 3x3 pixels
-- text_blur_type = 2 - 5x5 pixels
-- font_properties =  {"DejaVuLGCSans.ttf", 13, 0, 0, 0}  -- 字体属性 {"字体", 大小, 阴影 X 偏移, 阴影 Y 偏移, 模糊类型}
font_properties =  {"DejaVuLGCSans.ttf", 12, 2, 2, 0}

local aircraft_symbol_near  =  "⬡" --U+02C4  -- 飞行器近处
local aircraft_symbol_far   =  "⬡" --U+02C4  -- 飞行器远处

local ground_symbol_near    = "☐"  --U+02C9  -- 地面单位近处
local ground_symbol_far     = "□"  --U+02C9  -- 地面单位远处

local navy_symbol_near      = "︺"  --U+02DC  -- 海上近处
local navy_symbol_far       = "┉"  --U+02DC  -- 海上远处

local weapon_symbol_near    = "△"  --U+02C8  --  武器近处
local weapon_symbol_far     = "^"  --U+02C8  --  武器远处

local function dot_symbol(blending,opacity)
    return {"˙","CenterBottom", blending or 1.0 , opacity  or 0.1}
end

-- 不同显示程度的显示内容 显示程度变量名 = "%显示内容 %n换行 %显示内容"
local NAME 				   = "%N"
local NAME_DISTANCE_PLAYER = "%N%n %D%n %P"
local NAME_DISTANCE        = "%N%n %D"
local DISTANCE             =   "%n %D"

-- Text shadow color in {red, green, blue, alpha} format, volume from 0 up to 255  -- 文本阴影颜色为（R，G，B，阿尔法）格式，色相从0至255
-- alpha will by multiplied by opacity value for corresponding distance  -- 阿尔法是以距离决定不透明度。
local text_shadow_color = {128, 128, 128, 255}
local text_blur_color 	= {0, 0, 255, 255}

local EMPTY = {"", "LeftBottom", 1, 1, 0, 0}


if 		IS_DOT then    -- 如果点设置为真遵循以下条件（只显点）

AirFormat = {
--[distance]		= {format, alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[150]	= EMPTY,
[9260]	= {aircraft_symbol_near	, "CenterCenter"	,0.75	, 0.7	, 0	, 2},
[27780]	= {aircraft_symbol_far	, "CenterCenter"	,0.75	, 0.25	, 0	, 2},
[55560]	= dot_symbol(0,0.25),
}

GroundFormat = {
--[distance]		= {format , alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[10]	= EMPTY,
[3074]	= {ground_symbol_near	,"CenterCenter"	,0.75	, 0.7	, -3	, 0},
[18520]	= {ground_symbol_far	,"CenterCenter"	,0.75	, 0.5	, -3	, 0},
}

NavyFormat = {
--[distance]		= {format, alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[450]	= EMPTY,
[9260]	= {navy_symbol_near				,"CenterCenter"	,0.75	, 0.7	, -3	, 0},
[18520]	= {navy_symbol_far 				,"CenterCenter"	,0.75	, 0.5	, -3	, 0},
}

WeaponFormat = {
--[distance]		= {format ,alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[50]	    = EMPTY,
[9260]	= {weapon_symbol_near					,"CenterCenter"	,0.75	, 0.7	, -3	, 0},
[18520]	= {weapon_symbol_far					,"CenterCenter"	,0.75	, 0.5	, -3	, 0},
[37040]	=  dot_symbol(0.75, 0.1),
}

elseif IS_ABBREVIATED then          -- 如果点设置为假，并且简写设置为真遵循以下条件（简写显示下）

AirFormat = {
--[distance]		= {format, alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[150]	= EMPTY,
[9628]	= {aircraft_symbol_near..NAME		, "LeftBottom"	,0.75	, 0.7	, 0	, 0},
[27780]	= {aircraft_symbol_near..DISTANCE	, "LeftBottom"	,0.75	, 0.5	, -3	, 0},
[55560]	= {aircraft_symbol_far				, "CenterCenter"	,0.25	, 0.25	, -3	, 0},
}

GroundFormat = {
--[distance]		= {format , alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[150]	= EMPTY,
[3074]	= {ground_symbol_near..NAME		,"LeftBottom"	,0.75	, 0.7	, -3	, 0},
[18520]	= {ground_symbol_far			,"CenterCenter"	,0.75	, 0.5	, -3	, 0},
[37040]	=  dot_symbol(0.75, 0.1),
}

NavyFormat = {
--[distance]		= {format, alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[450]	= EMPTY,
[9260]	= {navy_symbol_near ..NAME				,"LeftBottom"	,0.75	, 0.7	, -3	, 0},
[27780]	= {navy_symbol_far  ..NAME  				,"LeftBottom"	,0.75	, 0.5	, -3	, 0},
[37040]	= dot_symbol(0.75,0.1),
}

WeaponFormat = {
--[distance]		= {format ,alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[50]	    = EMPTY,
[9260]	= {weapon_symbol_near ..NAME			,"LeftBottom"	,0.75	, 0.7	, -3	, 0},
[18520]	= {weapon_symbol_near					,"CenterCenter"	,0.75	, 0.5	, -3	, 0},
[37040]	= {weapon_symbol_far					,"CenterCenter"	,0.25	, 0.25	, -3	, 0},
}

else              -- 如果都不是以下条件（完整显示下）

AirFormat = {
--[distance]		= {format, alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[150]	= EMPTY,
[9628]	= {aircraft_symbol_near..NAME_DISTANCE_PLAYER	, "LeftBottom"	,0.75	, 0.7	, -3	, 0},       -- 5海里内近标..显示程度变量名
[27780]	= {aircraft_symbol_near..NAME_DISTANCE			, "LeftBottom"	,0.75	, 0.7	, -3	, 0},       -- 15海里内近标..显示程度变量名
[55560]	= {aircraft_symbol_near..DISTANCE				, "LeftBottom"	,0.25	, 0.7	, -3	, 0},       -- 30海里内近标..显示程度变量名
[92600]	= {aircraft_symbol_far							, "CenterCenter"	,0.25	, 0.5	, 0	, 0},       -- 50海里内远标
}

GroundFormat = {
--[distance]		= {format , alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[150]	= EMPTY,
[3074]	= {ground_symbol_near..NAME_DISTANCE_PLAYER		,"LeftBottom"	,0.75	, 0.7	, -3	, 0},       -- 3海里内近标..显示程度变量名
[18520]	= {ground_symbol_near..NAME_DISTANCE			,"LeftBottom"	,0.75	, 0.5	, -3	, 0},       -- 10海里内近标..显示程度变量名
[37040]	= {ground_symbol_far                            ,"CenterCenter"	,0.25	, 0.3	, 0	, 0}            -- 20海里内近标..显示程度变量名
}

NavyFormat = {
--[distance]		= {format, alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[450]	= EMPTY,
[9260]	= {navy_symbol_near ..NAME_DISTANCE				,"LeftBottom"	,0.75	, 0.7	, -3	, 0},   -- 5海里
[27780]	= {navy_symbol_far  ..DISTANCE  				,"LeftBottom"	,0.75	, 0.5	, -3	, 0},   -- 15海里
[37040]	= dot_symbol(0.75,0.1),
}

WeaponFormat = {
--[distance]		= {format ,alignment, color_blending_k, opacity, shift_in_pixels_x, shift_in_pixels_y}
[50]	    = EMPTY,
[9260]	= {weapon_symbol_near ..NAME_DISTANCE			,"LeftBottom"	,0.75	, 0.7	, -3	, 0},       -- 5海里
[18520]	= {weapon_symbol_near ..DISTANCE				,"LeftBottom"	,0.75	, 0.5	, -3	, 0},       -- 10海里
[37040]	= {weapon_symbol_far							,"CenterCenter"	,0.25	, 0.25	, -3	, 0},       -- 20海里
}

end

PointFormat = { 
[1e10]	= {"%N%n%D", "LeftBottom"},
}

-- Colors in {red, green, blue} format, volume from 0 up to 255

--ColorAliesSide   = {66, 66,66}
--ColorEnemiesSide = {66, 66,66}
--ColorAliesSide   = {140, 201,255}
--ColorEnemiesSide = {11, 213,55}

-- 不同阵营标签颜色
ColorAliesSide   = {255, 45, 0}    -- 红方阵营 RGB
ColorEnemiesSide = {0, 255,0}    -- 蓝方阵营 RGB
ColorUnknown     = {50, 50, 50} -- will be blend at distance with coalition color

-- 不同阵营阴影颜色
ShadowColorNeutralSide 	= {0,0,0,0}
ShadowColorAliesSide	= {0,0,0,0}
ShadowColorEnemiesSide 	= {0,0,0,0}
ShadowColorUnknown 		= {0,0,0,0}

-- 不同阵营模糊颜色
BlurColorNeutralSide 	= {255,255,255,255}
BlurColorAliesSide		= {50,0  ,0  ,255}
BlurColorEnemiesSide	= {0  ,0,50  ,255}
BlurColorUnknown		= {50 ,50 ,50 ,255}
