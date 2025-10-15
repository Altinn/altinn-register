
CREATE OR REPLACE FUNCTION register.upsert_party(
  INOUT p_uuid register.party.uuid%TYPE,
  INOUT p_id register.party.id%TYPE,
  INOUT p_user_ids bigint[],
  IN s_username boolean,
  INOUT p_username text,
  INOUT p_party_type register.party.party_type%TYPE,
  INOUT p_display_name register.party.display_name%TYPE,
  INOUT p_person_identifier register.party.person_identifier%TYPE,
  INOUT p_organization_identifier register.party.organization_identifier%TYPE,
  INOUT p_created register.party.created%TYPE,
  INOUT p_updated register.party.updated%TYPE,
  IN s_is_deleted boolean,
  INOUT p_is_deleted register.party.is_deleted%TYPE,
  IN s_deleted_at boolean,
  INOUT p_deleted_at register.party.deleted_at%TYPE,
  IN s_owner boolean,
  INOUT p_owner register.party.owner%TYPE,
  OUT o_version_id register.party.version_id%TYPE
)
AS $$
DECLARE
  o_party register.party%ROWTYPE;
BEGIN
  -- Get existing party by UUID if it exists
  SELECT p.*
  INTO o_party
  FROM register.party p
  WHERE p.uuid = p_uuid
  FOR NO KEY UPDATE;

  -- If the party does not exist, insert it and return it
  IF NOT FOUND THEN
    -- Assign defaults to optional fields
    IF NOT s_is_deleted THEN
      p_is_deleted := FALSE;
    END IF;

    IF NOT s_deleted_at THEN
      p_deleted_at := NULL;
    END IF;
    
    IF NOT s_owner THEN
      p_owner := NULL;
    END IF;

    -- Validate required fields

    -- Insert new party
    INSERT INTO register.party (uuid, id, party_type, display_name, person_identifier, organization_identifier, created, updated, is_deleted, deleted_at, "owner")
    VALUES (p_uuid, p_id, p_party_type, p_display_name, p_person_identifier, p_organization_identifier, p_created, p_updated, p_is_deleted, p_deleted_at, p_owner)
    RETURNING * INTO o_party;

  ELSE
    -- Validate that the updated party does not modify any immutable fields
    IF o_party.id <> p_id THEN
      RAISE EXCEPTION 'Cannot update immutable field "id" on party, existing: %, updated: %, party_uuid: %', o_party.id, p_id, p_uuid
        USING ERRCODE = 'ZZ001'
            , SCHEMA = 'register'
            , TABLE = 'party'
            , COLUMN = 'id';
    END IF;

    IF o_party.party_type <> p_party_type THEN
      RAISE EXCEPTION 'Cannot update immutable field "party_type" on party, existing: %, updated: %, party_uuid: %', o_party.party_type, p_party_type, p_uuid
        USING ERRCODE = 'ZZ001'
            , SCHEMA = 'register'
            , TABLE = 'party'
            , COLUMN = 'party_type';
    END IF;

    IF o_party.person_identifier <> p_person_identifier THEN
      RAISE EXCEPTION 'Cannot update immutable field "person_identifier" on party, existing: %, updated: %, party_uuid: %', o_party.person_identifier, p_person_identifier, p_uuid
        USING ERRCODE = 'ZZ001'
            , SCHEMA = 'register'
            , TABLE = 'party'
            , COLUMN = 'person_identifier';
    END IF;

    IF o_party.organization_identifier <> p_organization_identifier THEN
      RAISE EXCEPTION 'Cannot update immutable field "organization_identifier" on party, existing: %, updated: %, party_uuid: %', o_party.organization_identifier, p_organization_identifier, p_uuid
        USING ERRCODE = 'ZZ001'
            , SCHEMA = 'register'
            , TABLE = 'party'
            , COLUMN = 'organization_identifier';
    END IF;

    IF s_owner THEN
      IF o_party.owner <> p_owner THEN
        RAISE EXCEPTION 'Cannot update immutable field "owner" on party, existing: %, updated: %, party_uuid: %', o_party.owner, p_owner, p_uuid
          USING ERRCODE = 'ZZ001'
              , SCHEMA = 'register'
              , TABLE = 'party'
              , COLUMN = 'owner';
      END IF;
    END IF;

    UPDATE register.party p
    SET display_name = p_display_name
      , updated = p_updated
      , is_deleted = CASE WHEN s_is_deleted THEN p_is_deleted ELSE p.is_deleted END
      , deleted_at = CASE WHEN s_deleted_at THEN p_deleted_at ELSE p.deleted_at END
    WHERE p.uuid = p_uuid
    RETURNING * INTO o_party;
  END IF;

  -- If the party is deleted, clear username such that it can be reused by a person who absorbed a self-identified-user
  IF p_is_deleted THEN
    p_username := NULL;
  END IF;

  -- Upsert the user
  IF p_user_ids IS NOT NULL THEN
    SELECT *
    FROM register.upsert_user(p_uuid, p_is_deleted, p_user_ids, s_username, p_username)
    INTO p_user_ids, p_username;
  ELSEIF p_username IS NOT NULL THEN
    RAISE EXCEPTION 'Cannot set username without user_ids, party_uuid: %', p_uuid
      USING ERRCODE = 'ZZ002'
          , SCHEMA = 'register'
          , TABLE = 'party';
  END IF;

  p_uuid := o_party.uuid;
  p_id := o_party.id;
  p_party_type := o_party.party_type;
  p_display_name := o_party.display_name;
  p_person_identifier := o_party.person_identifier;
  p_organization_identifier := o_party.organization_identifier;
  p_created := o_party.created;
  p_updated := o_party.updated;
  p_is_deleted := o_party.is_deleted;
  p_deleted_at := o_party.deleted_at;
  p_owner := o_party.owner;
  o_version_id := o_party.version_id;
END;
$$ LANGUAGE plpgsql;
