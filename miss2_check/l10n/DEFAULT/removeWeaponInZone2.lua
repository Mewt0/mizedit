-- rmWeaponInZone("AIM_54A_Mk47", {"testUnitName", 3000})
-- rmWeaponInZone("AIM_54A_Mk60", {"testUnitName", 3000})
-- rmWeaponInZone("AIM_54C_Mk47", {"testUnitName", 3000})
-- rmWeaponInZone("AIM-120C", "TestZone")
-- rmWeaponInZone("AIM-9X", "TestZone")


rmWIZ = {
    -- ["AIM-120C"] = {["zoneName"] = "noAMRAAMHere"}
}

local checkInterval = 1

local idNum = 0
local function add_event_handler(f)
	local handler = {}
	idNum = idNum + 1
	handler.id = idNum
	handler.f = f
	function handler:onEvent(event)
		self.f(event)
	end
	world.addEventHandler(handler)
	return handler.id
end

function rmWeaponInZone(weaponName, zone)
    rmWIZ[weaponName] = {}
    if type(zone) == 'string' then
        rmWIZ[weaponName].zoneName = zone
    elseif type(zone) == 'table' then
        if type(zone[1]) == 'string' and type(zone[2]) == 'number' then
            rmWIZ[weaponName].unitName = zone[1]
            rmWIZ[weaponName].radius = zone[2]
        end
    end
end

local function distance2D(VecA, VecB)
    return math.sqrt(math.pow(VecA.x - VecB.x, 2) + math.pow(VecA.z - VecB.z, 2))
end

local function handleWeaponLaunch(event)
    if event.id == world.event.S_EVENT_SHOT then
        local WeaponDesc = event.weapon:getDesc()
        local typeName = WeaponDesc.typeName
        local displayName = WeaponDesc.displayName
        for name, missileList in pairs(rmWIZ) do
            if name == displayName or name == typeName then
                table.insert(missileList, event.weapon)
            end
        end
	end
end

local function checkWeaponPositon()
    timer.scheduleFunction(checkWeaponPositon, {}, timer.getTime() + checkInterval)
    for weaponType, missileList in pairs(rmWIZ) do
        local point = {x = 0, y = 0, z = 0}
        local radius = 0
        local validZone = false
        if missileList.zoneName then
            local zone = trigger.misc.getZone(missileList.zoneName)
            point = zone.point
            radius = zone.radius
            validZone = true
        elseif missileList.unitName and missileList.radius then
            local unit = Unit.getByName(missileList.unitName)
            if unit then
                point = Unit.getByName(missileList.unitName):getPoint()
                radius = missileList.radius
                validZone = true
            else
                rmWIZ[weaponType] = nil
            end
        end
        if validZone then
            local missileToRemove = {}
            for index, missile in ipairs(missileList) do
                if missile:isExist() then
                    local missilePos = missile:getPoint()
                    if distance2D(missilePos, point) < radius then
                        missile:destroy()
                        table.insert(missileToRemove, index)
                    end
                else
                    table.insert(missileToRemove, index)
                end
            end
            table.sort(missileToRemove,function(a,b) return a>b end)
            for i, index in pairs(missileToRemove) do
                table.remove(missileList, index)
            end
        end
    end
end

add_event_handler(handleWeaponLaunch)
checkWeaponPositon()